using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Cars;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AutoLot.Tests.Listings;

/// <summary>
/// Сідер демонстраційних даних цілком.
///
/// Перевіряємо дві обіцянки, заради яких він і переписувався. Перша: салони
/// існують як окремі записи, і оголошення дилерів належать їм — без цього
/// картка показувала «приватна особа» під назвою «Автосалон №3». Друга:
/// повторний запуск не додає нічого нового, але оживляє торги, яким вийшов
/// строк, — інакше через тиждень після наповнення вкладка «Аукціони» порожня.
/// </summary>
public class DemoDataSeederTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 9, 15, 9, 0, 0, TimeSpan.Zero);

    private readonly TestDatabase database = new();
    private readonly AutoLotDbContext context;

    public DemoDataSeederTests()
    {
        context = database.CreateContext();
        SeedReferenceData();
    }

    [Fact]
    public async Task Dealer_listings_belong_to_a_dealership()
    {
        await RunAsync(Start);

        var dealerships = await context.Dealerships.ToListAsync();

        Assert.NotEmpty(dealerships);

        // Кожен салон має власника: без нього ним нікому керувати.
        Assert.All(dealerships, dealership =>
            Assert.Contains(
                context.Set<Domain.Dealers.DealershipMember>().Local,
                member => member.DealershipId == dealership.Id
                    && member.Role == Domain.Dealers.DealershipRole.Owner));

        var dealerIds = await context.Users
            .Where(user => user.AccountType == AccountType.Dealer)
            .Select(user => user.Id)
            .ToListAsync();

        var orphans = await context.Listings
            .Where(listing => dealerIds.Contains(listing.SellerId) && listing.DealershipId == null)
            .CountAsync();

        Assert.Equal(0, orphans);
    }

    /// <summary>
    /// Приватні продавці салону не мають — інакше «тип продавця» перестав би
    /// бути фільтром: усі оголошення були б салонними.
    /// </summary>
    [Fact]
    public async Task Private_sellers_stay_private()
    {
        await RunAsync(Start);

        var privateIds = await context.Users
            .Where(user => user.AccountType == AccountType.Private)
            .Select(user => user.Id)
            .ToListAsync();

        var wrong = await context.Listings
            .Where(listing => privateIds.Contains(listing.SellerId) && listing.DealershipId != null)
            .CountAsync();

        Assert.Equal(0, wrong);
    }

    [Fact]
    public async Task Auctions_start_out_alive()
    {
        await RunAsync(Start);

        var auctions = await context.Auctions.ToListAsync();

        Assert.NotEmpty(auctions);
        Assert.All(auctions, auction => Assert.True(auction.EndsAt > Start, "лот має ще тривати"));
    }

    /// <summary>
    /// Найважливіший тест сесії: минув місяць, усі лоти давно закриті — і після
    /// наступного запуску застосунку вони знову живі.
    /// </summary>
    [Fact]
    public async Task A_month_later_the_auctions_are_alive_again()
    {
        await RunAsync(Start);

        var later = Start.AddDays(30);

        // Те, що зробив би планувальник за цей місяць: строк вийшов, лот закрито.
        foreach (var auction in await context.Auctions.Include(item => item.Listing).ToListAsync())
        {
            auction.Status = AuctionStatus.Ended;
            auction.Listing.Status = ListingStatus.Archived;
        }

        await context.SaveChangesAsync();

        await RunAsync(later);

        var auctions = await context.Auctions.Include(item => item.Listing).ToListAsync();

        Assert.All(auctions, auction =>
        {
            Assert.Equal(AuctionStatus.Active, auction.Status);
            Assert.True(auction.EndsAt > later, "лот має закінчуватися в майбутньому");
            Assert.Equal(ListingStatus.Active, auction.Listing.Status);
        });
    }

    [Fact]
    public async Task A_second_run_adds_no_listings()
    {
        await RunAsync(Start);

        var before = await context.Listings.CountAsync();

        await RunAsync(Start.AddDays(30));

        Assert.Equal(before, await context.Listings.CountAsync());
    }

    /// <summary>
    /// Ставки з минулих торгів не переносяться: вони стосувалися вже закритого
    /// лота. Якби лишилися, історія показувала б ціну, якої ніхто не пропонує.
    /// </summary>
    [Fact]
    public async Task Bids_from_the_previous_round_do_not_survive()
    {
        await RunAsync(Start);

        var oldBidIds = await context.Bids.Select(bid => bid.Id).ToListAsync();

        Assert.NotEmpty(oldBidIds);

        await RunAsync(Start.AddDays(30));

        var survivors = await context.Bids
            .Where(bid => oldBidIds.Contains(bid.Id))
            .CountAsync();

        Assert.Equal(0, survivors);
    }

    /// <summary>
    /// Під кожним лотом, якому належить розмова, вона є, а під мовчазним — ні.
    /// </summary>
    [Fact]
    public async Task Every_talkative_lot_has_a_conversation()
    {
        await RunAsync(Start);

        await AssertConversationsAsync();
    }

    /// <summary>
    /// Вада, яку цей тест і стереже. База засіяна ще до появи коментарів, і
    /// один лот устиг закінчитися. Перезапуск торгів дописував розмову йому
    /// першим, а одноразове «дописування» бачило цей коментар і вирішувало,
    /// що робити вже нічого, — тож решта лотів, які й так ішли, лишалися
    /// мовчазними. Так і вийшло: розмова під дев'ятьма лотами з двадцяти шести.
    /// </summary>
    [Fact]
    public async Task Lots_that_were_already_running_get_a_conversation_too()
    {
        await RunAsync(Start);

        // Така база, якою вона була до появи коментарів: жодного.
        await context.AuctionComments.ExecuteDeleteAsync();

        var auctions = await context.Auctions.OrderBy(auction => auction.ListingId).ToListAsync();

        // Без двох таких лотів тест нічого не довів би: вада проявлялася лише
        // тоді, коли поруч із перезапущеним лотом є ще один, що йде далі.
        Assert.True(
            auctions.Count(auction => !DemoComments.IsQuiet(auction.ListingId)) >= 2,
            "у наборі замало лотів, які мають розмовляти");

        auctions[0].EndsAt = Start;
        await context.SaveChangesAsync();

        await RunAsync(Start.AddMinutes(1));

        await AssertConversationsAsync();
    }

    /// <summary>
    /// Повторний запуск не дописує розмову вдруге: інакше після кожного
    /// перезапуску застосунку під лотами множилися б однакові репліки.
    /// </summary>
    [Fact]
    public async Task A_second_run_leaves_conversations_as_they_are()
    {
        await RunAsync(Start);

        var before = await context.AuctionComments.Select(comment => comment.Id).OrderBy(id => id).ToListAsync();

        await RunAsync(Start);

        var after = await context.AuctionComments.Select(comment => comment.Id).OrderBy(id => id).ToListAsync();

        Assert.NotEmpty(before);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task The_counter_matches_the_history_in_the_database()
    {
        await RunAsync(Start);

        var auctions = await context.Auctions.Include(auction => auction.Bids).ToListAsync();

        Assert.All(auctions, auction => Assert.Equal(auction.Bids.Count, auction.BidCount));
    }

    // ─────────────────────────── Оснащення ───────────────────────────

    /// <summary>
    /// Кожен активний демо-лот: є під ним розмова рівно тоді, коли він не мовчить навмисно.
    /// </summary>
    private async Task AssertConversationsAsync()
    {
        var lots = await context.Auctions
            .Where(auction => auction.Status == AuctionStatus.Active)
            .Select(auction => auction.ListingId)
            .ToListAsync();

        var talking = await context.AuctionComments
            .Select(comment => comment.ListingId)
            .Distinct()
            .ToListAsync();

        Assert.NotEmpty(lots);
        Assert.All(lots, id => Assert.Equal(!DemoComments.IsQuiet(id), talking.Contains(id)));
    }

    /// <summary>
    /// Запускає сідер так само, як це робить застосунок при старті, тільки з
    /// маленьким набором: двадцяти оголошень досить, щоб серед них трапилися
    /// і торги, і салони, а малювати двісті заглушок було б довго.
    /// </summary>
    private async Task RunAsync(DateTimeOffset now)
    {
        var options = Options.Create(new DemoDataOptions
        {
            Enabled = true,
            ListingCount = 20,
        });

        var seeder = new DemoDataSeeder(
            context,
            TestIdentity.CreateUserManager(context),
            new NullPhotoStorage(),
            new FixedRate(),
            new FixedClock(now),
            options,
            NullLogger<DemoDataSeeder>.Instance);

        await seeder.SeedAsync();
    }

    /// <summary>
    /// Довідники, без яких сідер нічого не створить: ролі, місто, марка з
    /// моделлю. Решта — опції, країни — необов'язкові, і сідер це переживає.
    /// </summary>
    private void SeedReferenceData()
    {
        context.Roles.Add(new Role { Name = RoleNames.User, NormalizedName = RoleNames.User.ToUpperInvariant() });

        // Назви міст і областей лежать окремо, у таблиці перекладів: тут вони
        // не потрібні, сідеру досить ідентифікатора.
        var region = new Region { Code = "kyiv-region" };
        context.Regions.Add(region);

        context.Cities.Add(new City
        {
            Code = "kyiv",
            Region = region,

            // Сідер бере лише помітні міста — у селі демо-оголошення виглядало б дивно.
            Population = 2_900_000,
        });

        var make = new Make { Name = "Honda", Slug = "honda" };
        context.Makes.Add(make);
        context.Models.Add(new Model { Name = "Pilot", Slug = "pilot", Make = make });

        context.SaveChanges();
    }

    public void Dispose()
    {
        context.Dispose();
        database.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Сховище, яке нічого не зберігає: перевіряємо дані, а не файли.</summary>
    private sealed class NullPhotoStorage : IPhotoStorage
    {
        public Task<string> SaveAsync(
            string relativeDirectory,
            string fileName,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken = default) =>
            Task.FromResult($"{relativeDirectory}/{fileName}");

        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FixedRate : IExchangeRateProvider
    {
        public Task<decimal> GetRateToUahAsync(
            Currency currency,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(currency is Currency.Uah ? 1m : 42m);
    }
}
