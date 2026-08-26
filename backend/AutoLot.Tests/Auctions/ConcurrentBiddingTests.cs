using AutoLot.Application.Auctions;
using AutoLot.Domain.Auctions;
using AutoLot.Domain.Cars;
using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Auctions;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoLot.Tests.Auctions;

/// <summary>
/// Головний ризик проєкту (SPEC §5): дві ставки, що прийшли одночасно, не
/// повинні пройти по одній ціні.
///
/// Тест іде на СПРАВЖНІЙ PostgreSQL, бо перевіряє саме поведінку бази —
/// блокування рядка. На SQLite він зеленів би, нічого не доводячи.
/// Потрібен запущений сервер: той самий, на якому працює застосунок.
/// </summary>
public class ConcurrentBiddingTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Стільки ставок за SPEC §5 має витримати лот.</summary>
    private const int Bidders = 50;

    private const decimal StartPrice = 5_000m;

    private PostgresTestDatabase database = null!;

    private long listingId;

    private readonly RecordingNotifier notifier = new();

    public async Task InitializeAsync()
    {
        database = await PostgresTestDatabase.CreateAsync();
        listingId = await SeedAuctionAsync();
    }

    public async Task DisposeAsync()
    {
        await database.DisposeAsync();
    }

    [Fact]
    public async Task Fifty_identical_bids_leave_exactly_one_winner()
    {
        // Усі 50 називають ту саму стелю — рівно стартову ціну. Пройти може
        // тільки перший: після нього мінімальна ставка вже вища на крок.
        var outcomes = await BidInParallelAsync(_ => StartPrice);

        var accepted = outcomes.Count(outcome => outcome.Accepted);
        var rejected = outcomes.Count(outcome => !outcome.Accepted);

        Assert.Equal(1, accepted);
        Assert.Equal(Bidders - 1, rejected);

        // Кожна відмова має бути саме порушенням правила («ставка замала»),
        // а не збоєм бази чи гонитвою, що впала сама собою.
        Assert.All(
            outcomes.Where(outcome => !outcome.Accepted),
            outcome => Assert.IsType<DomainRuleException>(outcome.Error));

        await using var context = database.CreateContext();
        var auction = context.Auctions.Single();

        Assert.Equal(StartPrice, auction.CurrentPrice);
        Assert.Equal(1, auction.BidCount);
        Assert.Single(context.Bids);
    }

    [Fact]
    public async Task Fifty_rising_bids_stay_consistent()
    {
        // Кожен наступний готовий заплатити більше. Хто саме встигне раніше —
        // непередбачувано, і це нормально: важливо, щоб підсумок був цілісним.
        var outcomes = await BidInParallelAsync(index => StartPrice + ((index + 1) * 500m));

        await using var context = database.CreateContext();
        var auction = context.Auctions.Single();
        var bids = context.Bids.OrderBy(bid => bid.Id).ToList();

        // Лічильник у лоті й справжня кількість рядків історії мають збігатися.
        // Розбіжність означала б утрачене оновлення — саме те, від чого
        // захищає блокування.
        Assert.Equal(bids.Count, auction.BidCount);

        // Ціна лота — це остання ставка в історії, а не якась проміжна.
        Assert.Equal(bids[^1].Amount, auction.CurrentPrice);

        // Переможець — той, чия стеля найвища серед тих, кого прийняли.
        var highestAccepted = outcomes
            .Where(outcome => outcome.Accepted)
            .Max(outcome => outcome.MaxAmount);

        Assert.Equal(highestAccepted, auction.LeaderMaxAmount);

        // Ціна не може перевищити стелю лідера — інакше він переплатив би.
        Assert.True(auction.CurrentPrice <= auction.LeaderMaxAmount);
    }

    [Fact]
    public async Task A_successful_bid_is_announced_to_everyone_watching()
    {
        await using var context = database.CreateContext();
        var service = new AuctionService(context, new FixedClock(Now), notifier, NullLogger<AuctionService>.Instance);

        await service.PlaceBidAsync(listingId, FirstBidderId, maxAmount: 8_000m);

        var update = Assert.Single(notifier.Updates);

        Assert.Equal(listingId, update.ListingId);
        Assert.Equal(StartPrice, update.CurrentPrice);
        Assert.Equal(FirstBidderId, update.LeaderId);
        Assert.Equal(1, update.BidCount);

        // У розсилці — новий рядок історії з іменем, а не з голим номером.
        var bid = Assert.Single(update.NewBids);
        Assert.Equal("bidder0", bid.BidderName);
        Assert.Equal(StartPrice, bid.Amount);
    }

    [Fact]
    public async Task A_rejected_bid_is_not_announced()
    {
        await using var context = database.CreateContext();
        var service = new AuctionService(context, new FixedClock(Now), notifier, NullLogger<AuctionService>.Instance);

        await Assert.ThrowsAsync<DomainRuleException>(
            () => service.PlaceBidAsync(listingId, FirstBidderId, maxAmount: 1m));

        // Інакше глядачі побачили б ціну, якої в базі немає.
        Assert.Empty(notifier.Updates);
    }

    [Fact]
    public async Task A_broken_channel_does_not_undo_an_accepted_bid()
    {
        await using var context = database.CreateContext();
        var service = new AuctionService(
            context,
            new FixedClock(Now),
            new FailingNotifier(),
            NullLogger<AuctionService>.Instance);

        // Розсилка падає, але ставка вже збережена — скасовувати її через
        // проблеми з каналом означало б покарати учасника за чужий збій.
        var details = await service.PlaceBidAsync(listingId, FirstBidderId, maxAmount: 8_000m);

        Assert.Equal(StartPrice, details.CurrentPrice);

        await using var fresh = database.CreateContext();
        Assert.Equal(1, fresh.Auctions.Single().BidCount);
    }

    [Fact]
    public async Task The_seller_cannot_bid_on_their_own_lot()
    {
        await using var context = database.CreateContext();
        var service = new AuctionService(context, new FixedClock(Now), notifier, NullLogger<AuctionService>.Instance);

        await Assert.ThrowsAsync<BiddingNotAllowedException>(
            () => service.PlaceBidAsync(listingId, SellerId, StartPrice));
    }

    /// <summary>
    /// Запускає всі ставки одночасно. Кожна дістає власний контекст, тобто
    /// власне з'єднання з базою: без цього ставки стали б у чергу всередині
    /// застосунку й жодної конкуренції не вийшло б.
    /// </summary>
    private async Task<IReadOnlyList<Outcome>> BidInParallelAsync(Func<int, decimal> maxAmountOf)
    {
        // Спільний бар'єр: усі задачі чекають одна одну й стартують разом.
        //
        // Стояти на ньому треба ПІСЛЯ того, як з'єднання з базою вже відкрите.
        // Спершу бар'єр стояв перед створенням контексту — і тест мовчки
        // втрачав сенс: поки останні задачі відкривали з'єднання, перша
        // встигала повністю відпрацювати, і жодної одночасності не виходило.
        using var ready = new CountdownEvent(Bidders);

        // TaskCompletionSource — це «обіцянка», яку можна виконати ззовні.
        // Задачі чекають на неї через await, тобто НЕ тримають потік.
        // Блокувальне очікування тут не годиться: п'ятдесят задач,
        // що стоять на потоках, вичерпують пул, і відкриття з'єднання
        // просто не має на чому продовжитися.
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(0, Bidders).Select(index => Task.Run(async () =>
        {
            var maxAmount = maxAmountOf(index);

            await using var context = database.CreateContext();
            var service = new AuctionService(context, new FixedClock(Now), notifier, NullLogger<AuctionService>.Instance);

            // Відкриваємо з'єднання завчасно, щоб на старті лишилася сама ставка.
            await context.Database.OpenConnectionAsync();

            ready.Signal();
            await start.Task;

            try
            {
                await service.PlaceBidAsync(listingId, FirstBidderId + index, maxAmount);

                return new Outcome(maxAmount, Accepted: true, Error: null);
            }
            catch (Exception error)
            {
                return new Outcome(maxAmount, Accepted: false, error);
            }
        })).ToList();

        // Чекаємо, поки всі 50 стануть на лінію, і аж тоді даємо старт.
        ready.Wait(TimeSpan.FromSeconds(30));
        start.SetResult();

        return await Task.WhenAll(tasks);
    }

    private const long SellerId = 1;

    private const long FirstBidderId = 100;

    private async Task<long> SeedAuctionAsync()
    {
        await using var context = database.CreateContext();

        context.Regions.Add(new Region { Id = 1, Code = "kyiv-region" });
        context.Cities.Add(new City { Id = 1, RegionId = 1, Code = "kyiv" });
        context.Makes.Add(new Make { Id = 1, Name = "BMW", Slug = "bmw" });
        context.Models.Add(new Model { Id = 1, MakeId = 1, Name = "X5", Slug = "x5" });

        context.Users.Add(NewUser(SellerId, "seller"));

        for (var index = 0; index < Bidders; index++)
        {
            context.Users.Add(NewUser(FirstBidderId + index, $"bidder{index}"));
        }

        var listing = new Listing
        {
            Title = "Лот для перевірки конкурентності",
            Description = "Опис",
            SellerId = SellerId,
            CityId = 1,
            Price = StartPrice,
            Currency = Currency.Usd,
            PriceUah = 210_000m,
            Type = ListingType.Auction,
            Status = ListingStatus.Active,
            Car = new Car
            {
                Year = 2020,
                MakeId = 1,
                ModelId = 1,
                FuelType = FuelType.Petrol,
                Transmission = TransmissionType.Automatic,
                Drivetrain = DrivetrainType.AllWheel,
                BodyType = BodyType.Crossover,
                Color = CarColor.Black,
            },
        };

        context.Listings.Add(listing);
        await context.SaveChangesAsync();

        context.Auctions.Add(new Auction
        {
            ListingId = listing.Id,
            Currency = Currency.Usd,
            StartPrice = StartPrice,
            CurrentPrice = StartPrice,
            StartsAt = Now,
            EndsAt = Now.AddDays(7),
            Status = AuctionStatus.Active,
        });

        await context.SaveChangesAsync();

        return listing.Id;
    }

    private static User NewUser(long id, string name) => new()
    {
        Id = id,
        UserName = $"{name}@example.com",
        Email = $"{name}@example.com",
        DisplayName = name,
    };

    private sealed record Outcome(decimal MaxAmount, bool Accepted, Exception? Error);
}
