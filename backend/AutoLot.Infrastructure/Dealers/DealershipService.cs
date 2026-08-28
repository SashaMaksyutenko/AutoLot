using System.Globalization;
using System.Text;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Dealers;
using AutoLot.Application.Dealers.Dtos;
using AutoLot.Domain.Common;
using AutoLot.Domain.Dealers;
using AutoLot.Domain.Enums;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Dealers;

/// <summary>
/// Автосалони та їхній персонал. Права всередині салону перевіряються тут за
/// записом у базі, а не за тим, що надіслав клієнт (SPEC §8).
/// </summary>
internal sealed class DealershipService(
    AutoLotDbContext dbContext,
    IDateTimeProvider clock,
    ICurrentLanguage language) : IDealershipService
{
    public async Task<DealershipDetails?> GetBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var dealership = await dbContext.Dealerships
            .AsNoTracking()
            .Include(item => item.City)
            .FirstOrDefaultAsync(item => item.Slug == slug, cancellationToken);

        if (dealership is null)
        {
            return null;
        }

        return await ToDetailsAsync(dealership, cancellationToken);
    }

    public async Task<DealershipDetails> CreateAsync(
        long founderId,
        CreateDealershipRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cityExists = await dbContext.Cities
            .AsNoTracking()
            .AnyAsync(city => city.Id == request.CityId, cancellationToken);

        if (!cityExists)
        {
            throw new DomainRuleException("Такого міста немає в довіднику.");
        }

        var dealership = new Dealership
        {
            Name = request.Name.Trim(),
            Slug = await UniqueSlugAsync(request.Name, cancellationToken),
            Description = request.Description?.Trim(),
            CityId = request.CityId,
        };

        // Засновник одразу стає власником: інакше салон лишився б без нікого,
        // хто може додати персонал, і його довелося б лагодити руками в базі.
        dealership.Members.Add(new DealershipMember
        {
            UserId = founderId,
            Role = DealershipRole.Owner,
            JoinedAt = clock.UtcNow,
        });

        dbContext.Dealerships.Add(dealership);

        // Тип акаунта має збігатися з дійсністю: людина, що завела салон,
        // більше не приватна особа, і ліміт у п'ять оголошень до неї не діє.
        var founder = await dbContext.Users
            .FirstOrDefaultAsync(user => user.Id == founderId, cancellationToken);

        if (founder is not null)
        {
            founder.AccountType = AccountType.Dealer;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.Entry(dealership).Reference(item => item.City).LoadAsync(cancellationToken);

        return await ToDetailsAsync(dealership, cancellationToken);
    }

    public async Task<IReadOnlyList<DealershipMembership>> GetMembershipsAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.DealershipMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId)
            .OrderBy(member => member.Dealership.Name)
            .Select(member => new DealershipMembership(
                member.DealershipId,
                member.Dealership.Name,
                member.Dealership.Slug,
                member.Role,
                member.Dealership.IsVerified))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StaffMember>> GetStaffAsync(
        long dealershipId,
        long actorId,
        CancellationToken cancellationToken = default)
    {
        // Склад персоналу — не публічна інформація: там пошта живих людей.
        await EnsureMemberAsync(dealershipId, actorId, cancellationToken);

        return await dbContext.DealershipMembers
            .AsNoTracking()
            .Where(member => member.DealershipId == dealershipId)
            .OrderByDescending(member => member.Role)
            .ThenBy(member => member.JoinedAt)
            .Select(member => new StaffMember(
                member.UserId,
                member.User.DisplayName,
                member.User.Email ?? string.Empty,
                member.Role,
                member.JoinedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task AddStaffAsync(
        long dealershipId,
        long actorId,
        string email,
        DealershipRole role,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnerAsync(dealershipId, actorId, cancellationToken);

        var normalized = email.Trim().ToUpperInvariant();

        var user = await dbContext.Users
            .FirstOrDefaultAsync(item => item.NormalizedEmail == normalized, cancellationToken)
            ?? throw new DomainRuleException("Користувача з такою поштою немає.");

        var alreadyThere = await dbContext.DealershipMembers
            .AsNoTracking()
            .AnyAsync(
                member => member.DealershipId == dealershipId && member.UserId == user.Id,
                cancellationToken);

        if (alreadyThere)
        {
            throw new DomainRuleException("Ця людина вже працює в салоні.");
        }

        dbContext.DealershipMembers.Add(new DealershipMember
        {
            DealershipId = dealershipId,
            UserId = user.Id,
            Role = role,
            JoinedAt = clock.UtcNow,
        });

        user.AccountType = AccountType.Dealer;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveStaffAsync(
        long dealershipId,
        long actorId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnerAsync(dealershipId, actorId, cancellationToken);

        var member = await dbContext.DealershipMembers
            .FirstOrDefaultAsync(
                item => item.DealershipId == dealershipId && item.UserId == userId,
                cancellationToken)
            ?? throw new DomainRuleException("Ця людина не працює в салоні.");

        // Останнього власника прибирати не можна — салон лишився б без нікого,
        // хто може керувати персоналом.
        if (member.Role == DealershipRole.Owner)
        {
            var owners = await dbContext.DealershipMembers
                .AsNoTracking()
                .CountAsync(
                    item => item.DealershipId == dealershipId && item.Role == DealershipRole.Owner,
                    cancellationToken);

            if (owners <= 1)
            {
                throw new DomainRuleException(
                    "Це єдиний власник салону. Спершу призначте іншого.");
            }
        }

        dbContext.DealershipMembers.Remove(member);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Оголошення при цьому НЕ чіпаємо: вони належать салону, а не людині.
        // Саме заради цього випадку модель і зроблена такою.
    }

    public async Task SetVerificationAsync(
        long dealershipId,
        long moderatorId,
        bool isVerified,
        CancellationToken cancellationToken = default)
    {
        var dealership = await dbContext.Dealerships
            .FirstOrDefaultAsync(item => item.Id == dealershipId, cancellationToken)
            ?? throw new DealershipNotFoundException(dealershipId.ToString(CultureInfo.InvariantCulture));

        if (isVerified)
        {
            dealership.Verify(moderatorId, clock.UtcNow);
        }
        else
        {
            dealership.RevokeVerification();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureMemberAsync(
        long dealershipId,
        long actorId,
        CancellationToken cancellationToken)
    {
        var isMember = await dbContext.DealershipMembers
            .AsNoTracking()
            .AnyAsync(
                member => member.DealershipId == dealershipId && member.UserId == actorId,
                cancellationToken);

        if (!isMember)
        {
            throw new DealershipAccessException("Ви не працюєте в цьому салоні.");
        }
    }

    private async Task EnsureOwnerAsync(
        long dealershipId,
        long actorId,
        CancellationToken cancellationToken)
    {
        var role = await dbContext.DealershipMembers
            .AsNoTracking()
            .Where(member => member.DealershipId == dealershipId && member.UserId == actorId)
            .Select(member => (DealershipRole?)member.Role)
            .FirstOrDefaultAsync(cancellationToken);

        if (role != DealershipRole.Owner)
        {
            throw new DealershipAccessException("Керувати персоналом може лише власник салону.");
        }
    }

    private async Task<DealershipDetails> ToDetailsAsync(
        Dealership dealership,
        CancellationToken cancellationToken)
    {
        var activeCount = await dbContext.Listings
            .AsNoTracking()
            .CountAsync(
                listing => listing.DealershipId == dealership.Id
                    && listing.Status == ListingStatus.Active,
                cancellationToken);

        var code = language.Code;

        var cityName = await dbContext.Cities
            .AsNoTracking()
            .Where(city => city.Id == dealership.CityId)
            .Select(city =>
                city.Translations.Where(t => t.Language == code).Select(t => t.Name).FirstOrDefault()
                ?? city.Translations.Where(t => t.Language == LanguageCodes.Default).Select(t => t.Name).FirstOrDefault()
                ?? city.Code)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return new DealershipDetails(
            dealership.Id,
            dealership.Name,
            dealership.Slug,
            dealership.Description,
            dealership.LogoPath,
            cityName,
            dealership.IsVerified,
            dealership.VerifiedAt,
            activeCount);
    }

    /// <summary>
    /// Перетворює назву на частину адреси: «Авто Плюс» → «avto-plyus».
    /// Якщо така вже зайнята, додає номер — два салони з однаковою назвою
    /// цілком можливі в різних містах.
    /// </summary>
    private async Task<string> UniqueSlugAsync(string name, CancellationToken cancellationToken)
    {
        var basis = Slugify(name);
        var candidate = basis;
        var suffix = 2;

        while (await dbContext.Dealerships.AnyAsync(item => item.Slug == candidate, cancellationToken))
        {
            candidate = $"{basis}-{suffix++}";
        }

        return candidate;
    }

    /// <summary>Українські літери в латиницю за спрощеною таблицею.</summary>
    private static readonly Dictionary<char, string> Translit = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "h", ['ґ'] = "g", ['д'] = "d",
        ['е'] = "e", ['є'] = "ye", ['ж'] = "zh", ['з'] = "z", ['и'] = "y", ['і'] = "i",
        ['ї'] = "yi", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n",
        ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u",
        ['ф'] = "f", ['х'] = "kh", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "shch",
        ['ь'] = "", ['ю'] = "yu", ['я'] = "ya", ['\''] = "",
    };

    private static string Slugify(string name)
    {
        var builder = new StringBuilder();

        foreach (var symbol in name.Trim().ToLowerInvariant())
        {
            if (Translit.TryGetValue(symbol, out var replacement))
            {
                builder.Append(replacement);
            }
            else if (char.IsAsciiLetterOrDigit(symbol))
            {
                builder.Append(symbol);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-') is { Length: > 0 } slug ? slug : "dealer";
    }
}
