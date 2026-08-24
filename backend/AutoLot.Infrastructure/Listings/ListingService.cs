using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Geo;
using AutoLot.Application.Listings;
using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Listings;

internal sealed class ListingService(
    AutoLotDbContext dbContext,
    IGeoCatalog geoCatalog,
    IExchangeRateProvider exchangeRates,
    ListingMapper mapper) : IListingService
{
    /// <summary>Приватній особі — п'ять активних оголошень, дилеру — без ліміту (SPEC §3).</summary>
    private const int PrivateSellerLimit = 5;

    public async Task<long> CreateAsync(
        long sellerId,
        CreateListingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Ліміт тут не перевіряємо: чернетка нікому не видима й місця у видачі
        // не займає. Перевірка чекає на подання до модерації.
        await EnsureLocationExistsAsync(request.CityId, request.CityDistrictId, cancellationToken);
        await EnsureCarReferencesExistAsync(request.Car, cancellationToken);

        var listing = new Listing
        {
            SellerId = sellerId,
            Type = request.Type,
            Status = ListingStatus.Draft,
        };

        ApplyCommonFields(
            listing,
            request.Title,
            request.Description,
            request.CityId,
            request.CityDistrictId,
            request.IsNegotiable,
            request.AcceptsTrade,
            request.IsUrgent);

        await ApplyPriceAsync(listing, request.Price, request.Currency, cancellationToken);

        listing.Car = new Car();
        ApplyCarSpecification(listing.Car, request.Car);

        dbContext.Listings.Add(listing);
        await dbContext.SaveChangesAsync(cancellationToken);

        return listing.Id;
    }

    public async Task UpdateAsync(
        long listingId,
        long actorId,
        UpdateListingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var listing = await LoadForWriteAsync(listingId, cancellationToken);
        EnsureOwner(listing, actorId);

        if (!listing.IsEditable)
        {
            throw new Domain.Common.DomainRuleException(
                "Редагувати можна лише чернетку або відхилене оголошення.");
        }

        await EnsureLocationExistsAsync(request.CityId, request.CityDistrictId, cancellationToken);
        await EnsureCarReferencesExistAsync(request.Car, cancellationToken);

        ApplyCommonFields(
            listing,
            request.Title,
            request.Description,
            request.CityId,
            request.CityDistrictId,
            request.IsNegotiable,
            request.AcceptsTrade,
            request.IsUrgent);

        await ApplyPriceAsync(listing, request.Price, request.Currency, cancellationToken);

        ApplyCarSpecification(listing.Car, request.Car);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ListingDetails?> GetAsync(
        long listingId,
        long? actorId,
        bool actorIsModerator,
        CancellationToken cancellationToken = default)
    {
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Include(item => item.Seller)
            .Include(item => item.Car).ThenInclude(car => car.Make)
            .Include(item => item.Car).ThenInclude(car => car.Model)
            .Include(item => item.Car).ThenInclude(car => car.Generation)
            .Include(item => item.Car).ThenInclude(car => car.Features)
            .Include(item => item.Car).ThenInclude(car => car.Photos)

            // Тут одразу ДВА списки — опції комплектації й фотографії. Одним
            // запитом база повернула б їх усі можливі поєднання: 20 опцій ×
            // 15 фото = 300 рядків замість 35, і кожне поле оголошення в них
            // повторилося б 300 разів. AsSplitQuery розбиває це на кілька
            // окремих запитів, які EF потім склеює в пам'яті.
            .AsSplitQuery()
            .FirstOrDefaultAsync(item => item.Id == listingId, cancellationToken);

        if (listing is null)
        {
            return null;
        }

        // Чуже неопубліковане оголошення не просто закрите — його «немає».
        // Інакше за кодом відповіді можна було б перебрати чернетки інших.
        var isPublic = listing.Status is ListingStatus.Active or ListingStatus.Sold;
        var isOwner = actorId is { } id && id == listing.SellerId;

        if (!isPublic && !isOwner && !actorIsModerator)
        {
            return null;
        }

        // Свої перегляди не рахуємо: інакше лічильник накручував би автор,
        // щоразу відкриваючи власне оголошення.
        if (isPublic && !isOwner)
        {
            // ExecuteUpdate замість читання-зміни-запису: інкремент виконує
            // сама база, тож два одночасні перегляди не загублять один одного.
            await dbContext.Listings
                .Where(item => item.Id == listingId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(item => item.ViewCount, item => item.ViewCount + 1),
                    cancellationToken);

            listing.ViewCount++;
        }

        return await mapper.ToDetailsAsync(listing, isOwner || actorIsModerator, cancellationToken);
    }

    public async Task<IReadOnlyList<ListingSummary>> GetOwnAsync(
        long sellerId,
        ListingStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.SellerId == sellerId);

        if (status is { } wanted)
        {
            query = query.Where(listing => listing.Status == wanted);
        }

        return await mapper.ToSummariesAsync(
            query.OrderByDescending(listing => listing.CreatedAt),
            cancellationToken);
    }

    public async Task SubmitForModerationAsync(
        long listingId,
        long actorId,
        CancellationToken cancellationToken = default)
    {
        var listing = await LoadForWriteAsync(listingId, cancellationToken);
        EnsureOwner(listing, actorId);

        // Ліміт перевіряємо саме тут: чернеток може бути скільки завгодно,
        // місце в ліміті займає лише те, що йде у видачу.
        await EnsureLimitNotReachedAsync(actorId, cancellationToken, listing.Id);

        listing.SubmitForModeration();

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkSoldAsync(
        long listingId,
        long actorId,
        CancellationToken cancellationToken = default)
    {
        var listing = await LoadForWriteAsync(listingId, cancellationToken);
        EnsureOwner(listing, actorId);

        listing.MarkSold();

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveAsync(
        long listingId,
        long actorId,
        CancellationToken cancellationToken = default)
    {
        var listing = await LoadForWriteAsync(listingId, cancellationToken);
        EnsureOwner(listing, actorId);

        listing.Archive();

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteDraftAsync(
        long listingId,
        long actorId,
        CancellationToken cancellationToken = default)
    {
        var listing = await LoadForWriteAsync(listingId, cancellationToken);
        EnsureOwner(listing, actorId);

        if (listing.Status is not ListingStatus.Draft)
        {
            throw new Domain.Common.DomainRuleException(
                "Видалити можна лише чернетку; опубліковане оголошення архівують.");
        }

        dbContext.Listings.Remove(listing);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Listing> LoadForWriteAsync(long listingId, CancellationToken cancellationToken)
    {
        var listing = await dbContext.Listings
            .Include(item => item.Car).ThenInclude(car => car.Features)
            .FirstOrDefaultAsync(item => item.Id == listingId, cancellationToken);

        return listing ?? throw new ListingNotFoundException(listingId);
    }

    private static void EnsureOwner(Listing listing, long actorId)
    {
        if (listing.SellerId != actorId)
        {
            throw new ListingAccessException("Це оголошення належить іншому користувачеві.");
        }
    }

    private async Task EnsureLimitNotReachedAsync(
        long sellerId,
        CancellationToken cancellationToken,
        long? ignoreListingId = null)
    {
        var isDealer = await dbContext.Users
            .Where(user => user.Id == sellerId)
            .Select(user => user.AccountType == AccountType.Dealer)
            .FirstOrDefaultAsync(cancellationToken);

        if (isDealer)
        {
            return;
        }

        var active = await dbContext.Listings
            .Where(listing => listing.SellerId == sellerId
                && listing.Id != ignoreListingId
                && (listing.Status == ListingStatus.Active
                    || listing.Status == ListingStatus.PendingModeration))
            .CountAsync(cancellationToken);

        if (active >= PrivateSellerLimit)
        {
            throw new Domain.Common.DomainRuleException(
                $"Приватна особа може мати не більше {PrivateSellerLimit} активних оголошень. " +
                "Архівуйте старі або перейдіть на дилерський акаунт.");
        }
    }

    private async Task EnsureLocationExistsAsync(
        long cityId,
        long? cityDistrictId,
        CancellationToken cancellationToken)
    {
        if (!await geoCatalog.LocationExistsAsync(cityId, cityDistrictId, cancellationToken))
        {
            throw new ListingDataException(
                "Такого міста немає або вказаний район належить іншому місту.");
        }
    }

    /// <summary>
    /// Ідентифікатори марки, моделі, покоління, країн і опцій приходять від
    /// клієнта. Мало того, що вони можуть не існувати — модель може належати
    /// іншій марці, а покоління іншій моделі, і зовнішні ключі бази цього не
    /// помітять.
    /// </summary>
    private async Task EnsureCarReferencesExistAsync(
        CarSpecification car,
        CancellationToken cancellationToken)
    {
        var modelBelongsToMake = await dbContext.Models
            .AnyAsync(model => model.Id == car.ModelId && model.MakeId == car.MakeId, cancellationToken);

        if (!modelBelongsToMake)
        {
            throw new ListingDataException("Обрана модель не належить цій марці.");
        }

        if (car.GenerationId is { } generationId)
        {
            var generationBelongsToModel = await dbContext.Generations
                .AnyAsync(
                    generation => generation.Id == generationId && generation.ModelId == car.ModelId,
                    cancellationToken);

            if (!generationBelongsToModel)
            {
                throw new ListingDataException("Обране покоління не належить цій моделі.");
            }
        }

        await EnsureCountryExistsAsync(car.ManufacturerCountryId, "країна-виробник", cancellationToken);
        await EnsureCountryExistsAsync(car.ImportedFromCountryId, "країна пригону", cancellationToken);

        if (car.FeatureIds.Count > 0)
        {
            var known = await dbContext.Features
                .CountAsync(feature => car.FeatureIds.Contains(feature.Id), cancellationToken);

            if (known != car.FeatureIds.Count)
            {
                throw new ListingDataException("Серед обраних опцій є невідомі.");
            }
        }
    }

    private async Task EnsureCountryExistsAsync(
        long? countryId,
        string field,
        CancellationToken cancellationToken)
    {
        if (countryId is not { } id)
        {
            return;
        }

        if (!await dbContext.Countries.AnyAsync(country => country.Id == id, cancellationToken))
        {
            throw new ListingDataException($"Невідома {field}.");
        }
    }

    private static void ApplyCommonFields(
        Listing listing,
        string title,
        string description,
        long cityId,
        long? cityDistrictId,
        bool isNegotiable,
        bool acceptsTrade,
        bool isUrgent)
    {
        listing.Title = title.Trim();
        listing.Description = description.Trim();
        listing.CityId = cityId;
        listing.CityDistrictId = cityDistrictId;
        listing.IsNegotiable = isNegotiable;
        listing.AcceptsTrade = acceptsTrade;
        listing.IsUrgent = isUrgent;
    }

    private async Task ApplyPriceAsync(
        Listing listing,
        decimal price,
        Currency currency,
        CancellationToken cancellationToken)
    {
        var rate = await exchangeRates.GetRateToUahAsync(currency, cancellationToken);

        listing.Price = price;
        listing.Currency = currency;

        // Знімок у гривні рахуємо на момент збереження; щоденна задача
        // перерахує його, коли зміниться курс.
        listing.PriceUah = decimal.Round(price * rate, 2);
    }

    private static void ApplyCarSpecification(Car car, CarSpecification specification)
    {
        car.Vin = string.IsNullOrWhiteSpace(specification.Vin) ? null : specification.Vin.ToUpperInvariant();
        car.Year = specification.Year;
        car.Condition = specification.Condition;
        car.MakeId = specification.MakeId;
        car.ModelId = specification.ModelId;
        car.GenerationId = specification.GenerationId;
        car.Mileage = specification.Mileage;
        car.OwnerCount = specification.OwnerCount;
        car.FuelType = specification.FuelType;
        car.EngineVolume = specification.EngineVolume;
        car.EnginePower = specification.EnginePower;
        car.FuelConsumptionCity = specification.FuelConsumptionCity;
        car.FuelConsumptionHighway = specification.FuelConsumptionHighway;
        car.FuelConsumptionCombined = specification.FuelConsumptionCombined;
        car.BatteryCapacity = specification.BatteryCapacity;
        car.ElectricRange = specification.ElectricRange;
        car.ChargingPort = specification.ChargingPort;
        car.Transmission = specification.Transmission;
        car.Drivetrain = specification.Drivetrain;
        car.BodyType = specification.BodyType;
        car.Color = specification.Color;
        car.IsMetallic = specification.IsMetallic;
        car.SeatCount = specification.SeatCount;
        car.DoorCount = specification.DoorCount;
        car.EcologyStandard = specification.EcologyStandard;
        car.ManufacturerCountryId = specification.ManufacturerCountryId;
        car.ImportedFromCountryId = specification.ImportedFromCountryId;
        car.IsCustomsCleared = specification.IsCustomsCleared;
        car.IsLocatedInUkraine = specification.IsLocatedInUkraine;
        car.WasInAccident = specification.WasInAccident;
        car.DamageState = specification.DamageState;
        car.PaintCondition = specification.PaintCondition;
        car.HasServiceBook = specification.HasServiceBook;
        car.IsGarageKept = specification.IsGarageKept;
        car.IsOnCredit = specification.IsOnCredit;

        SyncFeatures(car, specification.FeatureIds);
    }

    /// <summary>
    /// Приводить набір опцій до надісланого: прибирає зняті, додає нові,
    /// решту не чіпає. Простіше було б видалити всі й вставити заново, але
    /// тоді кожне збереження переписувало б рядки без потреби.
    /// </summary>
    private static void SyncFeatures(Car car, IReadOnlyList<long> featureIds)
    {
        var wanted = featureIds.ToHashSet();

        foreach (var link in car.Features.Where(link => !wanted.Contains(link.FeatureId)).ToList())
        {
            car.Features.Remove(link);
        }

        var present = car.Features.Select(link => link.FeatureId).ToHashSet();

        foreach (var featureId in wanted.Where(id => !present.Contains(id)))
        {
            car.Features.Add(new CarFeature { FeatureId = featureId });
        }
    }
}
