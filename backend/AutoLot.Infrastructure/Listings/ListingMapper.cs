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
                listing.Seller.AccountType),
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
            listing.ViewCount);
    }

    public async Task<IReadOnlyList<ListingSummary>> ToSummariesAsync(
        IQueryable<Listing> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var code = language.Code;

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
                listing.PublishedAt))
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
}
