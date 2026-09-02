using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Geo;
using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Common;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Перетворює сутності на те, що бачить клієнт. Винесено з сервісу окремо:
/// читання й запис — різні задачі, і змішувати їх в одному класі означало б
/// дати йому дві причини для зміни.
/// </summary>
internal sealed class ListingMapper(
    AutoLotDbContext dbContext,
    ICurrentLanguage language,
    ICurrentUser currentUser,
    IGeoCatalog geoCatalog)
{
    public async Task<ListingDetails> ToDetailsAsync(
        Listing listing,
        bool includePrivateFields,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(listing);

        var location = await geoCatalog.GetLocationAsync(
            listing.CityId,
            listing.CityDistrictId,
            cancellationToken);

        var car = listing.Car;
        var featureIds = car.Features.Select(link => link.FeatureId).ToList();

        return new ListingDetails(
            listing.Id,
            listing.Title,
            listing.Description,
            listing.Type,
            listing.Status,
            listing.Price,
            listing.Currency,
            listing.PriceUah,
            listing.IsNegotiable,
            listing.AcceptsTrade,
            listing.IsUrgent,
            location,
            new SellerSummary(
                listing.Seller.Id,
                listing.Seller.DisplayName,
                listing.Seller.AccountType,

                // Гостю номер не віддаємо взагалі — не ховаємо на клієнті,
                // а не кладемо у відповідь. Сховане на клієнті знаходять
                // за секунду, переглянувши те, що прийшло з сервера.
                currentUser.IsAuthenticated ? listing.Seller.PhoneNumber : null,
                await RatingOfAsync(listing.SellerId, cancellationToken)),
            new CarDetails(
                car.Vin,
                car.Year,
                car.Condition,
                car.Make.Name,
                car.Model.Name,
                car.Generation?.Name,
                car.Mileage,
                car.OwnerCount,
                car.FuelType,
                car.EngineVolume,
                car.EnginePower,
                car.FuelConsumptionCity,
                car.FuelConsumptionHighway,
                car.FuelConsumptionCombined,
                car.BatteryCapacity,
                car.ElectricRange,
                car.ChargingPort,
                car.Transmission,
                car.Drivetrain,
                car.BodyType,
                car.Color,
                car.IsMetallic,
                car.SeatCount,
                car.DoorCount,
                car.EcologyStandard,
                await GetCountryNameAsync(car.ManufacturerCountryId, cancellationToken),
                await GetCountryNameAsync(car.ImportedFromCountryId, cancellationToken),
                car.IsCustomsCleared,
                car.IsLocatedInUkraine,
                car.WasInAccident,
                car.DamageState,
                car.PaintCondition,
                car.HasServiceBook,
                car.IsGarageKept,
                car.IsOnCredit,
                await GetFeatureNamesAsync(featureIds, cancellationToken)),

            // Головне фото першим, решта — у заданому автором порядку.
            [
                .. car.Photos
                    .OrderByDescending(photo => photo.IsPrimary)
                    .ThenBy(photo => photo.SortOrder)
                    .Select(photo => new ListingPhoto(
                        photo.Id,
                        photo.Path,
                        photo.ThumbnailPath,
                        photo.SortOrder,
                        photo.IsPrimary)),
            ],
            listing.PublishedAt,
            listing.ExpiresAt,

            // Причину відмови бачать лише автор і модератор: стороннім знати,
            // за що оголошення не пройшло, не треба.
            includePrivateFields ? listing.RejectionReason : null,
            listing.ViewCount,
            await IsFavoriteAsync(listing.Id, cancellationToken),
            listing.Dealership is { } dealership
                ? new DealerBadge(dealership.Name, dealership.Slug, dealership.IsVerified)
                : null);
    }

    /// <summary>
    /// Чи в обраному це оголошення в того, хто зараз дивиться. Гостю окремий
    /// запит не робимо взагалі — відповідь відома наперед.
    /// </summary>
    private async Task<bool> IsFavoriteAsync(long listingId, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } viewerId)
        {
            return false;
        }

        return await dbContext.Favorites
            .AsNoTracking()
            .AnyAsync(
                favorite => favorite.UserId == viewerId && favorite.ListingId == listingId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ListingSummary>> ToSummariesAsync(
        IQueryable<Listing> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var code = language.Code;

        // Значення виймаємо в локальні змінні ДО побудови запиту: усе, що
        // всередині Select, EF перекладає в SQL, і виклик currentUser.Id там
        // перекласти неможливо — а локальна змінна стає звичайним параметром.
        //
        // Для гостя viewerId дорівнює null, і умова user_id = NULL у SQL не
        // збігається з жодним рядком. Тобто «не в обраному» виходить само
        // собою, без окремої гілки в коді.
        var viewerId = currentUser.Id;

        return await query
            .AsNoTracking()
            .Select(listing => new ListingSummary(
                listing.Id,
                listing.Title,
                listing.Type,
                listing.Status,
                listing.Price,
                listing.Currency,
                listing.PriceUah,
                listing.Car.Make.Name,
                listing.Car.Model.Name,
                listing.Car.Year,
                listing.Car.Mileage,
                listing.Car.FuelType,
                listing.Car.Transmission,
                listing.City.Translations.Where(t => t.Language == code).Select(t => t.Name).FirstOrDefault()
                    ?? listing.City.Translations.Where(t => t.Language == LanguageCodes.Default).Select(t => t.Name).FirstOrDefault()
                    ?? listing.City.Code,
                listing.Car.Photos
                    .Where(photo => photo.IsPrimary)
                    .Select(photo => photo.Path)
                    .FirstOrDefault(),
                listing.PublishedAt,

                // Підзапит EXISTS усередині тієї самої вибірки: жодного
                // додаткового звернення до бази на кожну картку.
                dbContext.Favorites.Any(favorite =>
                    favorite.UserId == viewerId && favorite.ListingId == listing.Id),

                // Дані салону їдуть тим самим запитом через зв'язок. Умова
                // потрібна, бо в приватної особи салону немає, і без неї EF
                // повернув би запис із порожніми полями замість null.
                listing.Dealership == null
                    ? null
                    : new DealerBadge(
                        listing.Dealership.Name,
                        listing.Dealership.Slug,
                        listing.Dealership.IsVerified)))
            .ToListAsync(cancellationToken);
    }

    private async Task<string?> GetCountryNameAsync(long? countryId, CancellationToken cancellationToken)
    {
        if (countryId is not { } id)
        {
            return null;
        }

        var code = language.Code;

        return await dbContext.Countries
            .AsNoTracking()
            .Where(country => country.Id == id)
            .Select(country =>
                country.Translations.Where(t => t.Language == code).Select(t => t.Name).FirstOrDefault()
                ?? country.Translations.Where(t => t.Language == LanguageCodes.Default).Select(t => t.Name).FirstOrDefault()
                ?? country.Code)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<string>> GetFeatureNamesAsync(
        List<long> featureIds,
        CancellationToken cancellationToken)
    {
        if (featureIds.Count == 0)
        {
            return [];
        }

        var code = language.Code;

        return await dbContext.Features
            .AsNoTracking()
            .Where(feature => featureIds.Contains(feature.Id))
            .OrderBy(feature => feature.SortOrder)
            .Select(feature =>
                feature.Translations.Where(t => t.Language == code).Select(t => t.Name).FirstOrDefault()
                ?? feature.Translations.Where(t => t.Language == LanguageCodes.Default).Select(t => t.Name).FirstOrDefault()
                ?? feature.Code)
            .ToListAsync(cancellationToken);
    }
/// <summary>
    /// Рейтинг продавця одним запитом. Не через IReviewService: мапер
    /// малює картку й не має тягти за собою ще один сервіс заради двох
    /// чисел — а сервіс відгуків, своєю чергою, не має знати про картку.
    /// </summary>
    private async Task<RatingSummary> RatingOfAsync(
        long sellerId,
        CancellationToken cancellationToken)
    {
        var stats = await dbContext.Reviews
            .AsNoTracking()
            .Where(review => review.SubjectId == sellerId)
            .GroupBy(review => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Sum = group.Sum(review => review.Rating),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (stats is null || stats.Count == 0)
        {
            // Нуль відгуків — не нуль зірок.
            return new RatingSummary(0, 0m);
        }

        return new RatingSummary(
            stats.Count,
            Math.Round((decimal)stats.Sum / stats.Count, 1, MidpointRounding.AwayFromZero));
    }
}
