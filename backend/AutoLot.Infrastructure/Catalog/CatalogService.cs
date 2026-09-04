using AutoLot.Application.Catalog;
using AutoLot.Application.Common;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Catalog;

/// <summary>
/// Пошук у каталозі. Фільтри накладаються по черзі на один <c>IQueryable</c>,
/// тож у базу йде рівно два запити — підрахунок і сторінка, — а не запит на
/// кожну умову. Умови, яких немає в запиті, не додаються взагалі: SQL
/// лишається рівно таким, як потрібно цьому пошукові.
/// </summary>
internal sealed class CatalogService(
    AutoLotDbContext dbContext,
    IExchangeRateProvider exchangeRates,
    ListingMapper mapper) : ICatalogService
{
    public async Task<PagedResult<ListingSummary>> SearchAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Каталог показує лише опубліковане. Це не фільтр, який можна зняти
        // параметром, а межа видимості.
        var listings = dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Status == ListingStatus.Active);

        listings = await ApplyPriceAsync(listings, query, cancellationToken);
        listings = ApplyText(listings, query);
        listings = ApplyModel(listings, query);
        listings = ApplyRanges(listings, query);
        listings = ApplySets(listings, query);
        listings = ApplyFlags(listings, query);
        listings = ApplyLocation(listings, query);
        listings = ApplySeller(listings, query);
        listings = ApplyFeatures(listings, query);

        var totalCount = await listings.CountAsync(cancellationToken);

        var page = Sort(listings, query.Sort)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize);

        var items = await mapper.ToSummariesAsync(page, cancellationToken);

        return new PagedResult<ListingSummary>(items, query.Page, query.PageSize, totalCount);
    }

    /// <summary>
    /// Ціна порівнюється в гривні, тож межі, введені в доларах, спершу
    /// переводимо за тим самим курсом, яким рахувався знімок ціни.
    /// </summary>
    private async Task<IQueryable<Listing>> ApplyPriceAsync(
        IQueryable<Listing> listings,
        CatalogQuery query,
        CancellationToken cancellationToken)
    {
        if (query.PriceFrom is null && query.PriceTo is null)
        {
            return listings;
        }

        var rate = await exchangeRates.GetRateToUahAsync(query.PriceCurrency, cancellationToken);

        if (query.PriceFrom is { } from)
        {
            var lower = from * rate;
            listings = listings.Where(listing => listing.PriceUah >= lower);
        }

        if (query.PriceTo is { } to)
        {
            var upper = to * rate;
            listings = listings.Where(listing => listing.PriceUah <= upper);
        }

        return listings;
    }

    private static IQueryable<Listing> ApplyText(IQueryable<Listing> listings, CatalogQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.Text))
        {
            return listings;
        }

        var text = query.Text.Trim();

        // ILike — пошук без урахування регістру засобами PostgreSQL, тож
        // «passat» і «Passat» дають однаковий результат. Транслітерації тут
        // немає: кирилицею назву моделі не знайти, бо в довіднику вона
        // латиницею. Це свідомо лишено на потім разом із повнотекстовим
        // пошуком — див. пункт 15 плану.
        return listings.Where(listing =>
            EF.Functions.ILike(listing.Title, $"%{text}%")
            || EF.Functions.ILike(listing.Car.Make.Name, $"%{text}%")
            || EF.Functions.ILike(listing.Car.Model.Name, $"%{text}%"));
    }

    private static IQueryable<Listing> ApplyModel(IQueryable<Listing> listings, CatalogQuery query)
    {
        if (query.MakeId is { } makeId)
        {
            listings = listings.Where(listing => listing.Car.MakeId == makeId);
        }

        if (query.ModelId is { } modelId)
        {
            listings = listings.Where(listing => listing.Car.ModelId == modelId);
        }

        if (query.GenerationId is { } generationId)
        {
            listings = listings.Where(listing => listing.Car.GenerationId == generationId);
        }

        return listings;
    }

    private static IQueryable<Listing> ApplyRanges(IQueryable<Listing> listings, CatalogQuery query)
    {
        if (query.YearFrom is { } yearFrom)
        {
            listings = listings.Where(listing => listing.Car.Year >= yearFrom);
        }

        if (query.YearTo is { } yearTo)
        {
            listings = listings.Where(listing => listing.Car.Year <= yearTo);
        }

        if (query.MileageFrom is { } mileageFrom)
        {
            listings = listings.Where(listing => listing.Car.Mileage >= mileageFrom);
        }

        if (query.MileageTo is { } mileageTo)
        {
            listings = listings.Where(listing => listing.Car.Mileage <= mileageTo);
        }

        if (query.EngineVolumeFrom is { } volumeFrom)
        {
            listings = listings.Where(listing => listing.Car.EngineVolume >= volumeFrom);
        }

        if (query.EngineVolumeTo is { } volumeTo)
        {
            listings = listings.Where(listing => listing.Car.EngineVolume <= volumeTo);
        }

        if (query.PowerFrom is { } powerFrom)
        {
            listings = listings.Where(listing => listing.Car.EnginePower >= powerFrom);
        }

        if (query.PowerTo is { } powerTo)
        {
            listings = listings.Where(listing => listing.Car.EnginePower <= powerTo);
        }

        // Витрату порівнюємо за змішаним циклом: міський і трасовий показники
        // без нього непорівнянні між авто, бо міряють різне.
        if (query.FuelConsumptionTo is { } consumptionTo)
        {
            listings = listings.Where(listing => listing.Car.FuelConsumptionCombined <= consumptionTo);
        }

        if (query.OwnerCountTo is { } ownersTo)
        {
            listings = listings.Where(listing => listing.Car.OwnerCount <= ownersTo);
        }

        if (query.SeatCountFrom is { } seatsFrom)
        {
            listings = listings.Where(listing => listing.Car.SeatCount >= seatsFrom);
        }

        if (query.SeatCountTo is { } seatsTo)
        {
            listings = listings.Where(listing => listing.Car.SeatCount <= seatsTo);
        }

        if (query.DoorCountFrom is { } doorsFrom)
        {
            listings = listings.Where(listing => listing.Car.DoorCount >= doorsFrom);
        }

        if (query.BatteryCapacityFrom is { } batteryFrom)
        {
            listings = listings.Where(listing => listing.Car.BatteryCapacity >= batteryFrom);
        }

        if (query.ElectricRangeFrom is { } rangeFrom)
        {
            listings = listings.Where(listing => listing.Car.ElectricRange >= rangeFrom);
        }

        return listings;
    }

    /// <summary>
    /// Набори значень — це «або»: обраний седан і універсал означає «седан
    /// АБО універсал», а не порожню видачу.
    /// </summary>
    private static IQueryable<Listing> ApplySets(IQueryable<Listing> listings, CatalogQuery query)
    {
        if (query.BodyTypes.Length > 0)
        {
            listings = listings.Where(listing => query.BodyTypes.Contains(listing.Car.BodyType));
        }

        if (query.FuelTypes.Length > 0)
        {
            listings = listings.Where(listing => query.FuelTypes.Contains(listing.Car.FuelType));
        }

        if (query.Transmissions.Length > 0)
        {
            listings = listings.Where(listing => query.Transmissions.Contains(listing.Car.Transmission));
        }

        if (query.Drivetrains.Length > 0)
        {
            listings = listings.Where(listing => query.Drivetrains.Contains(listing.Car.Drivetrain));
        }

        if (query.Colors.Length > 0)
        {
            listings = listings.Where(listing => query.Colors.Contains(listing.Car.Color));
        }

        if (query.DamageStates.Length > 0)
        {
            listings = listings.Where(listing => query.DamageStates.Contains(listing.Car.DamageState));
        }

        // Поля нижче необов'язкові, тож порівнюємо через Value: без нього
        // Contains по типу з питальним знаком не перекладається в SQL.
        if (query.PaintConditions.Length > 0)
        {
            listings = listings.Where(listing =>
                listing.Car.PaintCondition != null
                && query.PaintConditions.Contains(listing.Car.PaintCondition.Value));
        }

        if (query.EcologyStandards.Length > 0)
        {
            listings = listings.Where(listing =>
                listing.Car.EcologyStandard != null
                && query.EcologyStandards.Contains(listing.Car.EcologyStandard.Value));
        }

        if (query.ChargingPorts.Length > 0)
        {
            listings = listings.Where(listing =>
                listing.Car.ChargingPort != null
                && query.ChargingPorts.Contains(listing.Car.ChargingPort.Value));
        }

        return listings;
    }

    private static IQueryable<Listing> ApplyFlags(IQueryable<Listing> listings, CatalogQuery query)
    {
        if (query.Condition is { } condition)
        {
            listings = listings.Where(listing => listing.Car.Condition == condition);
        }

        if (query.WasInAccident is { } wasInAccident)
        {
            listings = listings.Where(listing => listing.Car.WasInAccident == wasInAccident);
        }

        if (query.IsCustomsCleared is { } customsCleared)
        {
            listings = listings.Where(listing => listing.Car.IsCustomsCleared == customsCleared);
        }

        if (query.IsLocatedInUkraine is { } inUkraine)
        {
            listings = listings.Where(listing => listing.Car.IsLocatedInUkraine == inUkraine);
        }

        if (query.ImportedFromCountryId is { } importedFrom)
        {
            listings = listings.Where(listing => listing.Car.ImportedFromCountryId == importedFrom);
        }

        if (query.ManufacturerCountryId is { } madeIn)
        {
            listings = listings.Where(listing => listing.Car.ManufacturerCountryId == madeIn);
        }

        if (query.IsMetallic is { } metallic)
        {
            listings = listings.Where(listing => listing.Car.IsMetallic == metallic);
        }

        if (query.HasServiceBook is { } serviceBook)
        {
            listings = listings.Where(listing => listing.Car.HasServiceBook == serviceBook);
        }

        if (query.IsGarageKept is { } garageKept)
        {
            listings = listings.Where(listing => listing.Car.IsGarageKept == garageKept);
        }

        if (query.IsOnCredit is { } onCredit)
        {
            listings = listings.Where(listing => listing.Car.IsOnCredit == onCredit);
        }

        if (query.CityDistrictId is { } cityDistrictId)
        {
            listings = listings.Where(listing => listing.CityDistrictId == cityDistrictId);
        }

        if (query.IsNegotiable is { } negotiable)
        {
            listings = listings.Where(listing => listing.IsNegotiable == negotiable);
        }

        if (query.AcceptsTrade is { } acceptsTrade)
        {
            listings = listings.Where(listing => listing.AcceptsTrade == acceptsTrade);
        }

        if (query.IsUrgent is { } urgent)
        {
            listings = listings.Where(listing => listing.IsUrgent == urgent);
        }


        if (query.Type is { } listingType)
        {
            listings = listings.Where(listing => listing.Type == listingType);
        }

        if (query.HasPhotos is { } hasPhotos)
        {
            listings = hasPhotos
                ? listings.Where(listing => listing.Car.Photos.Count > 0)
                : listings.Where(listing => listing.Car.Photos.Count == 0);
        }

        return listings;
    }

    /// <summary>
    /// Хто продає. Належність до салону визначається наявністю зв'язку, а не
    /// типом акаунта продавця: людина може працювати в салоні й водночас
    /// продавати власне авто від себе — такий лот салонним не є.
    /// </summary>
    private static IQueryable<Listing> ApplySeller(IQueryable<Listing> listings, CatalogQuery query)
    {
        // Вітрина конкретного салону перекриває решту умов про продавця:
        // питання «чий це лот» уже вирішено.
        if (query.DealershipId is { } dealershipId)
        {
            return listings.Where(listing => listing.DealershipId == dealershipId);
        }

        if (query.FromDealer is { } fromDealer)
        {
            listings = fromDealer
                ? listings.Where(listing => listing.DealershipId != null)
                : listings.Where(listing => listing.DealershipId == null);
        }

        if (query.VerifiedDealerOnly == true)
        {
            listings = listings.Where(listing =>
                listing.Dealership != null && listing.Dealership.IsVerified);
        }

        return listings;
    }

    private static IQueryable<Listing> ApplyLocation(IQueryable<Listing> listings, CatalogQuery query)
    {
        // Конкретне місто точніше за область, тож якщо є обидва — вистачить міста.
        if (query.CityId is { } cityId)
        {
            return listings.Where(listing => listing.CityId == cityId);
        }

        if (query.RegionId is { } regionId)
        {
            listings = listings.Where(listing => listing.City.RegionId == regionId);
        }

        return listings;
    }

    /// <summary>
    /// Опції — це «і»: обрані підігрів сидінь і парктроніки означають авто,
    /// де є обидві. Тому на кожну опцію накладається окрема умова існування,
    /// а не один Contains.
    /// </summary>
    private static IQueryable<Listing> ApplyFeatures(IQueryable<Listing> listings, CatalogQuery query)
    {
        foreach (var featureId in query.FeatureIds.Distinct())
        {
            var wanted = featureId;

            listings = listings.Where(listing =>
                listing.Car.Features.Any(link => link.FeatureId == wanted));
        }

        return listings;
    }

    private static IQueryable<Listing> Sort(IQueryable<Listing> listings, CatalogSort sort) => sort switch
    {
        CatalogSort.PriceAscending => listings.OrderBy(listing => listing.PriceUah).ThenBy(listing => listing.Id),
        CatalogSort.PriceDescending => listings.OrderByDescending(listing => listing.PriceUah).ThenBy(listing => listing.Id),
        CatalogSort.MileageAscending => listings.OrderBy(listing => listing.Car.Mileage).ThenBy(listing => listing.Id),
        CatalogSort.YearDescending => listings.OrderByDescending(listing => listing.Car.Year).ThenBy(listing => listing.Id),

        // Додатковий ключ за Id обов'язковий: без нього оголошення з однаковою
        // ціною чи датою можуть з'їхати між сторінками й показатися двічі.
        _ => listings.OrderByDescending(listing => listing.PublishedAt).ThenBy(listing => listing.Id),
    };
}
