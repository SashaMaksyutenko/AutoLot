using AutoLot.Application.Geo;
using AutoLot.Application.Listings.Dtos;
using AutoLot.Application.Users;
using AutoLot.Application.Users.Dtos;
using AutoLot.Domain.Enums;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Identity;

/// <summary>
/// Збирає профіль продавця для сторонніх.
///
/// Працює напряму з базою, без <c>UserManager</c>: той потрібен для паролів,
/// блокувань і підтверджень, а тут лише читання кількох полів. Зайва
/// залежність не безкоштовна — вона тягне в тести всю механіку Identity
/// заради запиту, який її не використовує.
/// </summary>
internal sealed class PublicProfileService(
    AutoLotDbContext dbContext,
    IGeoCatalog geoCatalog) : IPublicProfileService
{
    public async Task<PublicProfile?> GetAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.DisplayName,
                candidate.AccountType,
                candidate.CreatedAt,
                candidate.CityId,
                candidate.CityDistrictId,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        var location = user.CityId is { } cityId
            ? await geoCatalog.GetLocationAsync(cityId, user.CityDistrictId, cancellationToken)
            : null;

        // Рахуємо лише опубліковані: чернетки й архів — справа господаря,
        // стороннім їх не показують і не рахують.
        var activeListings = await dbContext.Listings
            .AsNoTracking()
            .CountAsync(
                listing => listing.SellerId == userId && listing.Status == ListingStatus.Active,
                cancellationToken);

        // Салон, у якому людина працює. Перший-ліпший: працювати у двох
        // салонах правила не забороняють, але показувати обидва в одному
        // рядку картки нема куди.
        var dealer = await dbContext.DealershipMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId)
            // Упорядкування обов'язкове: без нього база вільна віддати
            // будь-який рядок, і той самий профіль показував би то один
            // салон, то інший. EF на це справедливо попереджає.
            .OrderBy(member => member.DealershipId)
            .Select(member => new DealerBadge(
                member.Dealership.Name,
                member.Dealership.Slug,
                member.Dealership.IsVerified))
            .FirstOrDefaultAsync(cancellationToken);

        return new PublicProfile(
            user.Id,
            user.DisplayName,
            user.AccountType,
            user.CreatedAt,
            location?.CityName,
            await RatingQuery.OfAsync(dbContext, userId, cancellationToken),
            activeListings,
            dealer);
    }
}
