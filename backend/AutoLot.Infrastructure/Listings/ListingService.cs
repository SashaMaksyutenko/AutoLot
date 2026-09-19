using AutoLot.Application.Billing;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Geo;
using AutoLot.Application.Listings;
using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AutoLot.Domain.Common;

namespace AutoLot.Infrastructure.Listings;

internal sealed partial class ListingService(
    AutoLotDbContext dbContext,
    IGeoCatalog geoCatalog,
    IExchangeRateProvider exchangeRates,
    IDateTimeProvider clock,
    ListingMapper mapper,
    ListingAccess access,
    IListingAllowance allowance,
    ILogger<ListingService> logger) : IListingService
{
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

        // Подати від імені салону може лише той, хто в ньому працює. Перевірка
        // саме тут, а не у валідаторі: валідатор бачить лише тіло запиту, а
        // відповідь на це питання лежить у базі (SPEC §8).
        if (request.DealershipId is { } dealershipId
            && !await access.IsMemberAsync(dealershipId, sellerId, cancellationToken))
        {
            throw new ListingAccessException(MessageCodes.DealershipNotAMember);
        }

        var listing = new Listing
        {
            SellerId = sellerId,
            DealershipId = request.DealershipId,
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

        await ApplyPriceAsync(listing, request.Price, request.Currency, request.ReservePrice, cancellationToken);

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
        await EnsureCanManageAsync(listing, actorId, cancellationToken);

        if (!listing.IsEditable)
        {
            throw new Domain.Common.DomainRuleException(
                MessageCodes.ListingEditWrongStatus);
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

        await ApplyPriceAsync(listing, request.Price, request.Currency, request.ReservePrice, cancellationToken);

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
            .Include(item => item.Dealership)
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

        // «Свій» тепер означає і «мого салону»: менеджер має бачити чернетку
        // колеги так само, як власну.
        var isOwner = actorId is { } id
            && await access.CanManageAsync(listing, id, cancellationToken);

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

            if (actorId is { } viewerId)
            {
                await RememberViewAsync(viewerId, listingId, cancellationToken);
            }
        }

        return await mapper.ToDetailsAsync(listing, isOwner || actorIsModerator, cancellationToken);
    }

    public async Task<IReadOnlyList<ListingSummary>> GetRecentlyViewedAsync(
        long userId,
        int take,
        CancellationToken cancellationToken = default)
    {
        /*
          Знято з публікації або продане в історії не показуємо: людина
          побачила б рядок, який нікуди не веде.

          Передаємо саме ЗАПИТ, а не готовий список: мапувальник домальовує
          до кожного оголошення обране, салон і фото, і робить це одним
          походом у базу замість походу на кожен рядок.
        */
        var query = dbContext.ListingViews
            .AsNoTracking()
            .Where(view => view.UserId == userId && view.Listing.Status == ListingStatus.Active)
            .OrderByDescending(view => view.ViewedAt)
            .Take(take)
            .Select(view => view.Listing);

        return await mapper.ToSummariesAsync(query, cancellationToken);
    }

    /// <summary>
    /// Відмічає, що людина відкрила це оголошення.
    ///
    /// Повторний перегляд лише пересуває час: історія — це перелік авто, а
    /// не журнал кліків.
    /// </summary>
    private async Task RememberViewAsync(
        long userId,
        long listingId,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        var seen = await dbContext.ListingViews.FirstOrDefaultAsync(
            view => view.UserId == userId && view.ListingId == listingId,
            cancellationToken);

        if (seen is not null)
        {
            seen.ViewedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);

            return;
        }

        dbContext.ListingViews.Add(new ListingView
        {
            UserId = userId,
            ListingId = listingId,
            ViewedAt = now,
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            /*
              Той самий рядок устиг з'явитися паралельно — людина відкрила
              авто у двох вкладках або двічі клацнула. Обмеження бази це
              спіймало, і саме так і має бути; для нас же це не помилка:
              потрібне вже записано, лишається тільки не падати.
            */
            dbContext.ChangeTracker.Clear();

            return;
        }

        await TrimHistoryAsync(userId, cancellationToken);
    }

    /// <summary>
    /// Прибирає найдавніші пункти понад межу.
    ///
    /// Викликається лише після появи НОВОГО рядка: повторні перегляди
    /// довжини історії не міняють, тож і чистити після них нема чого.
    /// </summary>
    private async Task TrimHistoryAsync(long userId, CancellationToken cancellationToken)
    {
        var total = await dbContext.ListingViews
            .CountAsync(view => view.UserId == userId, cancellationToken);

        if (total <= ListingView.PerUserLimit)
        {
            return;
        }

        var extra = await dbContext.ListingViews
            .Where(view => view.UserId == userId)
            .OrderBy(view => view.ViewedAt)
            .Take(total - ListingView.PerUserLimit)
            .ToListAsync(cancellationToken);

        dbContext.ListingViews.RemoveRange(extra);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ListingDraft?> GetForEditAsync(
        long listingId,
        long actorId,
        CancellationToken cancellationToken = default)
    {
        // Include на місто потрібен рівно заради області: форма показує
        // спершу область, потім міста в ній.
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Include(item => item.City)
            .Include(item => item.Car).ThenInclude(car => car.Features)
            .FirstOrDefaultAsync(item => item.Id == listingId, cancellationToken);

        if (listing is null)
        {
            return null;
        }

        // Чуже оголошення для стороннього не існує — так само, як у GetAsync.
        // Кидати «немає прав» означало б підтвердити, що воно є.
        if (!await access.CanManageAsync(listing, actorId, cancellationToken))
        {
            return null;
        }

        var car = listing.Car;

        return new ListingDraft
        {
            Id = listing.Id,
            Title = listing.Title,
            Description = listing.Description,
            RegionId = listing.City.RegionId,
            CityId = listing.CityId,
            CityDistrictId = listing.CityDistrictId,
            Price = listing.Price,
            Currency = listing.Currency,
            ReservePrice = listing.ReservePrice,
            Type = listing.Type,
            IsNegotiable = listing.IsNegotiable,
            AcceptsTrade = listing.AcceptsTrade,
            IsUrgent = listing.IsUrgent,
            DealershipId = listing.DealershipId,
            Status = listing.Status,
            RejectionReason = listing.RejectionReason,
            Car = new CarSpecification
            {
                Vin = car.Vin,
                Year = car.Year,
                Condition = car.Condition,
                MakeId = car.MakeId,
                ModelId = car.ModelId,
                GenerationId = car.GenerationId,
                Mileage = car.Mileage,
                OwnerCount = car.OwnerCount,
                FuelType = car.FuelType,
                EngineVolume = car.EngineVolume,
                EnginePower = car.EnginePower,
                FuelConsumptionCity = car.FuelConsumptionCity,
                FuelConsumptionHighway = car.FuelConsumptionHighway,
                FuelConsumptionCombined = car.FuelConsumptionCombined,
                BatteryCapacity = car.BatteryCapacity,
                ElectricRange = car.ElectricRange,
                ChargingPort = car.ChargingPort,
                Transmission = car.Transmission,
                Drivetrain = car.Drivetrain,
                BodyType = car.BodyType,
                Color = car.Color,
                IsMetallic = car.IsMetallic,
                SeatCount = car.SeatCount,
                DoorCount = car.DoorCount,
                EcologyStandard = car.EcologyStandard,
                ManufacturerCountryId = car.ManufacturerCountryId,
                ImportedFromCountryId = car.ImportedFromCountryId,
                IsCustomsCleared = car.IsCustomsCleared,
                IsLocatedInUkraine = car.IsLocatedInUkraine,
                WasInAccident = car.WasInAccident,
                DamageState = car.DamageState,
                PaintCondition = car.PaintCondition,
                HasServiceBook = car.HasServiceBook,
                IsGarageKept = car.IsGarageKept,
                IsOnCredit = car.IsOnCredit,
                FeatureIds = [.. car.Features.Select(link => link.FeatureId)],
            },
        };
    }

    public async Task<IReadOnlyList<ListingSummary>> GetOwnAsync(
        long sellerId,
        ListingStatus? status,
        CancellationToken cancellationToken = default)
    {
        // «Мої оголошення» для менеджера салону — це і його власні, і всі
        // салонні: він відповідає за них нарівні з колегами.
        var dealershipIds = await access.DealershipIdsOfAsync(sellerId, cancellationToken);

        var query = ListingAccess.ManagedBy(
            dbContext.Listings.AsNoTracking(),
            sellerId,
            dealershipIds);

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
        await EnsureCanManageAsync(listing, actorId, cancellationToken);

        // Ліміт перевіряємо саме тут: чернеток може бути скільки завгодно,
        // місце в ліміті займає лише те, що йде у видачу.
        await EnsureLimitNotReachedAsync(actorId, cancellationToken, listing.Id);

        await EnsureVinIsNotOnSaleAsync(listing, cancellationToken);

        listing.SubmitForModeration();

        await dbContext.SaveChangesAsync(cancellationToken);

        LogSubmitted(logger, listingId, actorId);
    }

    /// <summary>
    /// Не дає виставити авто, яке вже продається під тим самим VIN.
    ///
    /// Один номер кузова — одне авто на світі, тож два оголошення про нього
    /// одночасно означають або дубль від самого продавця, або те, чого на
    /// майданчику бути не повинно: чужі фото під чужим номером. Це класична
    /// ознака шахрайства, і ловиться вона без жодного зовнішнього сервісу.
    ///
    /// Перевірка стоїть на ПОДАВАННІ, а не на створенні. Чернетку ніхто не
    /// бачить, місця у видачі вона не займає, а поки людина заповнює форму,
    /// попереднє оголошення цілком могло піти в архів. Відмовляти наперед
    /// означало б заважати там, де проблеми ще немає.
    ///
    /// Перепродаж це не ламає: у проданого чи архівного оголошення статус уже
    /// інший, тож новий власник виставить те саме авто спокійно.
    /// </summary>
    private async Task EnsureVinIsNotOnSaleAsync(Listing listing, CancellationToken cancellationToken)
    {
        var vin = listing.Car?.Vin;

        if (string.IsNullOrEmpty(vin))
        {
            return;
        }

        var alreadyOnSale = await dbContext.Listings
            .AsNoTracking()
            .AnyAsync(
                other => other.Id != listing.Id
                    && other.Car.Vin == vin
                    && (other.Status == ListingStatus.Active
                        || other.Status == ListingStatus.PendingModeration),
                cancellationToken);

        if (alreadyOnSale)
        {
            throw new Domain.Common.DomainRuleException(MessageCodes.CarVinDuplicate);
        }
    }

    public async Task<IReadOnlyList<ListingDetails>> GetManyAsync(
        IReadOnlyList<long> listingIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingIds);

        if (listingIds.Count == 0)
        {
            return [];
        }

        var listings = await dbContext.Listings
            .AsNoTracking()
            .Include(item => item.Seller)
            .Include(item => item.Dealership)
            .Include(item => item.Car).ThenInclude(car => car.Make)
            .Include(item => item.Car).ThenInclude(car => car.Model)
            .Include(item => item.Car).ThenInclude(car => car.Generation)
            .Include(item => item.Car).ThenInclude(car => car.Features)
            .Include(item => item.Car).ThenInclude(car => car.Photos)
            .AsSplitQuery()

            // Порівнювати можна лише опубліковане. Чужа чернетка не має
            // проступати навіть колонкою в таблиці.
            .Where(item => listingIds.Contains(item.Id) && item.Status == ListingStatus.Active)
            .ToListAsync(cancellationToken);

        var details = new List<ListingDetails>(listings.Count);

        foreach (var listing in listings)
        {
            details.Add(await mapper.ToDetailsAsync(
                listing,
                includePrivateFields: false,
                cancellationToken));
        }

        // Порядок беремо з запиту, а не з бази: людина додавала авто до
        // порівняння в певній послідовності, і колонки мають стояти так само.
        //
        // Distinct тут, а не лише в контролері: «одна колонка на авто» —
        // властивість самої операції, і кожен новий викликач не має
        // відкривати її заново.
        return
        [
            .. listingIds
                .Distinct()
                .Select(id => details.FirstOrDefault(item => item.Id == id))
                .Where(item => item is not null)
                .Select(item => item!),
        ];
    }

    public async Task ChangePriceAsync(
        long listingId,
        long actorId,
        decimal price,
        Currency currency,
        CancellationToken cancellationToken = default)
    {
        var listing = await LoadForWriteAsync(listingId, cancellationToken);
        await EnsureCanManageAsync(listing, actorId, cancellationToken);

        if (listing.Status is not ListingStatus.Active)
        {
            throw new Domain.Common.DomainRuleException(MessageCodes.PriceChangeWrongStatus);
        }

        // У торгах ціну веде сама сутність аукціону: її рухають ставки, і
        // переписати її збоку означало б підмінити результат торгів.
        if (listing.Type is ListingType.Auction)
        {
            throw new Domain.Common.DomainRuleException(MessageCodes.PriceChangeAuction);
        }

        await EnsureStartingPointAsync(listing, cancellationToken);

        await ApplyPriceAsync(listing, price, currency, reservePrice: null, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        LogPriceChanged(logger, listingId, actorId, price, currency);
    }

    /// <summary>
    /// Дописує в історію ціну, з якої оголошення починалося, — якщо історії
    /// ще немає зовсім.
    /// </summary>
    /// <remarks>
    /// Потрібно оголошенням, виставленим ДО того, як з'явилася історія цін:
    /// у них немає жодної точки. Без цього кроку перша зміна дала б одну
    /// точку — нову ціну, — і графік не показався б, бо малювати лінію з
    /// однієї точки нема з чого. Продавцеві довелося б змінити ціну двічі,
    /// щоб покупці побачили перше зниження.
    ///
    /// Датою ставимо момент публікації: саме тоді ця ціна й з'явилася перед
    /// покупцями.
    /// </remarks>
    private async Task EnsureStartingPointAsync(Listing listing, CancellationToken cancellationToken)
    {
        var hasHistory = await dbContext.Set<PriceChange>()
            .AnyAsync(change => change.ListingId == listing.Id, cancellationToken);

        if (hasHistory)
        {
            return;
        }

        dbContext.Set<PriceChange>().Add(new PriceChange
        {
            ListingId = listing.Id,
            Price = listing.Price,
            Currency = listing.Currency,
            PriceUah = listing.PriceUah,
            ChangedAt = listing.PublishedAt ?? clock.UtcNow,
        });
    }

    public async Task<IReadOnlyList<PriceHistoryPoint>> GetPriceHistoryAsync(
        long listingId,
        long? actorId,
        CancellationToken cancellationToken = default)
    {
        var listing = await dbContext.Listings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == listingId, cancellationToken);

        if (listing is null)
        {
            return [];
        }

        /*
            Та сама межа видимості, що й у самого оголошення: опубліковане
            бачать усі, решту — лише свої. Порожній перелік замість відмови
            навмисно: історія ціни це доповнення до картки, і окрема помилка
            тут нічого корисного не додала б.
        */
        var isPublic = listing.Status is ListingStatus.Active or ListingStatus.Sold;

        var isOwner = actorId is { } id
            && await access.CanManageAsync(listing, id, cancellationToken);

        if (!isPublic && !isOwner)
        {
            return [];
        }

        return await dbContext.Set<PriceChange>()
            .AsNoTracking()
            .Where(change => change.ListingId == listingId)
            .OrderBy(change => change.ChangedAt)
            .ThenBy(change => change.Id)
            .Select(change => new PriceHistoryPoint(
                change.Price,
                change.Currency,
                change.PriceUah,
                change.ChangedAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Наскільки ціна схожого авто може відрізнятися — в обидва боки.
    /// </summary>
    /// <remarks>
    /// Чверть — компроміс. Вужче, і для рідкісної моделі не знайдеться нічого;
    /// ширше, і поруч з авто за 20 тисяч опиниться авто за 35, яке тому, хто
    /// дивиться перше, просто не по кишені.
    /// </remarks>
    private const decimal SimilarPriceWindow = 0.25m;

    public async Task<IReadOnlyList<ListingSummary>> GetSimilarAsync(
        long listingId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var source = await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Id == listingId)
            .Select(listing => new
            {
                listing.Status,
                listing.PriceUah,
                listing.Car.MakeId,
                listing.Car.ModelId,
            })
            .FirstOrDefaultAsync(cancellationToken);

        /*
            Продане теж годиться як відправна точка — і навіть найкраще:
            людина, яка відкрила продане авто, саме й шукає, що купити
            замість нього.
        */
        if (source is null || source.Status is not (ListingStatus.Active or ListingStatus.Sold))
        {
            return [];
        }

        // Ціни порівнюємо в гривні: оголошення в доларах і в гривні мають
        // стояти поруч, якщо коштують однаково.
        var lowest = source.PriceUah * (1 - SimilarPriceWindow);
        var highest = source.PriceUah * (1 + SimilarPriceWindow);

        /*
            Одним запитом, а не двома («спершу модель, потім марка»). Сортування
            ставить ту саму модель наперед, далі йдуть інші моделі тієї ж
            марки, і в межах кожної групи — від найближчої ціни. Take забирає
            перші кілька: якщо однакових моделей вистачає, до марки справа
            просто не доходить.

            Порівняння ModelId == ... у OrderBy EF перекладає в CASE WHEN у
            SQL, а Math.Abs — у звичайну abs(), тож сортує сама база.
        */
        var query = dbContext.Listings
            .Where(listing => listing.Id != listingId && listing.Status == ListingStatus.Active)
            .Where(listing => listing.Car.MakeId == source.MakeId)
            .Where(listing => listing.PriceUah >= lowest && listing.PriceUah <= highest)
            .OrderBy(listing => listing.Car.ModelId == source.ModelId ? 0 : 1)
            .ThenBy(listing => Math.Abs(listing.PriceUah - source.PriceUah))
            .ThenBy(listing => listing.Id)
            .Take(take);

        return await mapper.ToSummariesAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<ListingSummary>> GetPurchasedAsync(
        long buyerId,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.BuyerId == buyerId)
            // Найсвіжіша покупка зверху: саме про неї згадають найшвидше,
            // і саме про неї писатимуть відгук.
            .OrderByDescending(listing => listing.SoldAt);

        return await mapper.ToSummariesAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<BuyerCandidate>> GetBuyerCandidatesAsync(
        long listingId,
        long actorId,
        CancellationToken cancellationToken = default)
    {
        var listing = await LoadForWriteAsync(listingId, cancellationToken);
        await EnsureCanManageAsync(listing, actorId, cancellationToken);

        return await CandidatesAsync(listing, cancellationToken);
    }

    public async Task MarkSoldAsync(
        long listingId,
        long actorId,
        long? buyerId,
        CancellationToken cancellationToken = default)
    {
        var listing = await LoadForWriteAsync(listingId, cancellationToken);
        await EnsureCanManageAsync(listing, actorId, cancellationToken);

        if (buyerId is { } wanted)
        {
            // Покупця беремо лише зі списку тих, з ким справді була справа.
            // Інакше продавець міг би приписати угоду будь-кому — а з появою
            // відгуків це означало б право написати відгук незнайомцю.
            var candidates = await CandidatesAsync(listing, cancellationToken);

            if (!candidates.Any(candidate => candidate.Id == wanted))
            {
                throw new ListingDataException(
                    MessageCodes.ListingBuyerNeverWrote);
            }
        }

        listing.MarkSold(clock.UtcNow, buyerId);

        await dbContext.SaveChangesAsync(cancellationToken);

        LogSold(logger, listingId, actorId, buyerId);
    }

    /// <summary>
    /// Хто міг купити. Для аукціонного лота відповідь одна — переможець:
    /// домовитися після торгів з кимось іншим означало б обійти самі торги.
    /// Для звичайного — усі, хто писав про це авто.
    /// </summary>
    private async Task<IReadOnlyList<BuyerCandidate>> CandidatesAsync(
        Listing listing,
        CancellationToken cancellationToken)
    {
        var winnerId = await dbContext.Auctions
            .AsNoTracking()
            .Where(auction => auction.ListingId == listing.Id)
            .Select(auction => auction.WinnerId)
            .FirstOrDefaultAsync(cancellationToken);

        if (winnerId is { } winner)
        {
            var name = await dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == winner)
                .Select(user => user.DisplayName)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            return [new BuyerCandidate(winner, name, listing.CreatedAt, IsAuctionWinner: true)];
        }

        return await dbContext.Conversations
            .AsNoTracking()
            .Where(conversation => conversation.ListingId == listing.Id)
            // Найсвіжіше листування зверху: з тим, хто писав учора, угода
            // ймовірніша, ніж із тим, хто питав місяць тому.
            .OrderByDescending(conversation => conversation.LastMessageAt)
            .Select(conversation => new BuyerCandidate(
                conversation.BuyerId,
                conversation.Buyer.DisplayName,
                conversation.LastMessageAt,
                false))
            .ToListAsync(cancellationToken);
    }

    public async Task ArchiveAsync(
        long listingId,
        long actorId,
        CancellationToken cancellationToken = default)
    {
        var listing = await LoadForWriteAsync(listingId, cancellationToken);
        await EnsureCanManageAsync(listing, actorId, cancellationToken);

        listing.Archive();

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <remarks>
    /// Єдина незворотна дія з оголошенням — тому єдина, яку тут пишемо
    /// рівнем Warning. Архівація зворотна, редагування лишає саме
    /// оголошення на місці, а видалене повернути вже нічим.
    /// </remarks>
    public async Task DeleteDraftAsync(
        long listingId,
        long actorId,
        CancellationToken cancellationToken = default)
    {
        var listing = await LoadForWriteAsync(listingId, cancellationToken);
        await EnsureCanManageAsync(listing, actorId, cancellationToken);

        if (listing.Status is not ListingStatus.Draft)
        {
            throw new Domain.Common.DomainRuleException(
                MessageCodes.ListingDeleteDraftOnly);
        }

        dbContext.Listings.Remove(listing);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogDraftDeleted(logger, listingId, actorId);
    }

    private async Task<Listing> LoadForWriteAsync(long listingId, CancellationToken cancellationToken)
    {
        var listing = await dbContext.Listings
            .Include(item => item.Car).ThenInclude(car => car.Features)
            .FirstOrDefaultAsync(item => item.Id == listingId, cancellationToken);

        return listing ?? throw new ListingNotFoundException(listingId);
    }

    /// <summary>
    /// «Це моє?» — питання, на яке з появою салонів відповідає не лише
    /// SellerId. Саме правило живе в <see cref="ListingAccess"/>, тут лише
    /// перетворення відповіді на виняток.
    /// </summary>
    private async Task EnsureCanManageAsync(
        Listing listing,
        long actorId,
        CancellationToken cancellationToken)
    {
        if (!await access.CanManageAsync(listing, actorId, cancellationToken))
        {
            throw new ListingAccessException(MessageCodes.ListingAccessOtherSeller);
        }
    }

    private async Task EnsureLimitNotReachedAsync(
        long sellerId,
        CancellationToken cancellationToken,
        long? ignoreListingId = null)
    {
        // Ліміт більше не константа: його дає чинний тарифний план. Порожня
        // відповідь означає «без межі» — так відповідає і найдорожчий тариф,
        // і дилерський акаунт.
        var limit = await allowance.GetListingLimitAsync(sellerId, cancellationToken);

        if (limit is not { } maximum)
        {
            return;
        }

        // Рахуємо лише ОСОБИСТІ оголошення. Салонні належать салону, і їх
        // обмежуватиме тарифний план салону — інакше менеджер вичерпав би
        // власний ліміт роботою, а приватні оголошення подавати вже не міг.
        var active = await dbContext.Listings
            .Where(listing => listing.SellerId == sellerId
                && listing.DealershipId == null
                && listing.Id != ignoreListingId
                && (listing.Status == ListingStatus.Active
                    || listing.Status == ListingStatus.PendingModeration))
            .CountAsync(cancellationToken);

        if (active >= maximum)
        {
            throw new Domain.Common.DomainRuleException(MessageCodes.ListingLimitReached)
                .With("limit", maximum);
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
                MessageCodes.PlaceCityOrDistrictInvalid);
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
            throw new ListingDataException(MessageCodes.CarModelWrongMake);
        }

        if (car.GenerationId is { } generationId)
        {
            var generationBelongsToModel = await dbContext.Generations
                .AnyAsync(
                    generation => generation.Id == generationId && generation.ModelId == car.ModelId,
                    cancellationToken);

            if (!generationBelongsToModel)
            {
                throw new ListingDataException(MessageCodes.CarGenerationWrongModel);
            }
        }

        await EnsureCountryExistsAsync(
            car.ManufacturerCountryId,
            MessageCodes.CarManufacturerCountryInvalid,
            cancellationToken);

        await EnsureCountryExistsAsync(
            car.ImportedFromCountryId,
            MessageCodes.CarImportedFromInvalid,
            cancellationToken);

        if (car.FeatureIds.Count > 0)
        {
            var known = await dbContext.Features
                .CountAsync(feature => car.FeatureIds.Contains(feature.Id), cancellationToken);

            if (known != car.FeatureIds.Count)
            {
                throw new ListingDataException(MessageCodes.CarFeaturesUnknownSome);
            }
        }
    }

    private async Task EnsureCountryExistsAsync(
        long? countryId,
        string messageCode,
        CancellationToken cancellationToken)
    {
        if (countryId is not { } id)
        {
            return;
        }

        if (!await dbContext.Countries.AnyAsync(country => country.Id == id, cancellationToken))
        {
            throw new ListingDataException(messageCode);
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
        decimal? reservePrice,
        CancellationToken cancellationToken)
    {
        var rate = await exchangeRates.GetRateToUahAsync(currency, cancellationToken);

        var priceUah = decimal.Round(price * rate, 2);

        RecordPriceChange(listing, price, currency, priceUah);

        listing.Price = price;
        listing.Currency = currency;

        // Резерв має сенс лише для торгів. Якщо тип змінили на фіксовану ціну,
        // раніше введена сума мусить зникнути, а не тихо лежати в базі.
        listing.ReservePrice = listing.Type == ListingType.Auction ? reservePrice : null;

        // Знімок у гривні рахуємо на момент збереження; щоденна задача
        // перерахує його, коли зміниться курс.
        listing.PriceUah = priceUah;
    }

    /// <summary>
    /// Дописує рядок в історію ціни — але лише коли ціна справді інша.
    /// </summary>
    /// <remarks>
    /// Порівнюємо і суму, і валюту: «5000 USD» та «5000 UAH» це різні ціни,
    /// хоч число однакове.
    ///
    /// Перший запис з'являється разом із оголошенням: без нього графік
    /// починався б із другої ціни, і перше зниження виглядало б так, ніби
    /// авто одразу виставили дешевшим.
    ///
    /// Редагування, яке ціни не торкнулося, історію не засмічує — інакше
    /// виправлена одруківка в описі додавала б у графік зайву точку.
    /// </remarks>
    private void RecordPriceChange(Listing listing, decimal price, Currency currency, decimal priceUah)
    {
        var isFirst = listing.Id == 0;

        if (!isFirst && listing.Price == price && listing.Currency == currency)
        {
            return;
        }

        dbContext.Set<PriceChange>().Add(new PriceChange
        {
            Listing = listing,
            Price = price,
            Currency = currency,
            PriceUah = priceUah,
            ChangedAt = clock.UtcNow,
        });
    }

    private static void ApplyCarSpecification(Car car, CarSpecification specification)
    {
        car.Vin = Vin.Normalize(specification.Vin);
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

    // ── Журнал ───────────────────────────────────────────────────────
    //
    // Пишемо не кожну дію, а ті, які комусь колись доведеться відновлювати
    // або оскаржувати: публікацію, угоду й видалення. Створення чернетки
    // й редагування сюди не входять — чернетку ніхто не бачить, а виправити
    // її можна скільки завгодно разів.

    [LoggerMessage(
        EventId = 200,
        Level = LogLevel.Information,
        Message = "Оголошення {ListingId} подано на модерацію користувачем {ActorId}")]
    private static partial void LogSubmitted(ILogger logger, long listingId, long actorId);

    [LoggerMessage(
        EventId = 201,
        Level = LogLevel.Information,
        Message = "Оголошення {ListingId} позначено проданим користувачем {ActorId}; покупець {BuyerId}")]
    private static partial void LogSold(
        ILogger logger,
        long listingId,
        long actorId,
        long? buyerId);

    [LoggerMessage(
        EventId = 202,
        Level = LogLevel.Warning,
        Message = "Чернетку {ListingId} видалено користувачем {ActorId}")]
    private static partial void LogDraftDeleted(ILogger logger, long listingId, long actorId);

    [LoggerMessage(
        EventId = 203,
        Level = LogLevel.Information,
        Message = "Ціну оголошення {ListingId} змінено користувачем {ActorId} на {Price} {Currency}")]
    private static partial void LogPriceChanged(
        ILogger logger,
        long listingId,
        long actorId,
        decimal price,
        Currency currency);
}
