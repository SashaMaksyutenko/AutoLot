using AutoLot.Application.Catalog;
using AutoLot.Domain.Cars;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Catalog;
using AutoLot.Infrastructure.Email;
using AutoLot.Infrastructure.Geo;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Infrastructure.Search;
using AutoLot.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AutoLot.Tests.Search;

/// <summary>
/// Розсилка про нові збіги.
///
/// Найважливіше тут — межа «нового». Помилка в ній дає одну з двох бід:
/// або людину заливає всім каталогом, або вона не дізнається ні про що.
/// </summary>
public class SavedSearchNotifierTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    private const long OwnerId = 1;

    private const long SellerId = 2;

    private readonly TestDatabase database = new();

    private readonly AutoLotDbContext context;

    private readonly RecordingEmailSender mail = new();

    public SavedSearchNotifierTests()
    {
        context = database.CreateContext();
        Seed();
    }

    [Fact]
    public async Task Nothing_is_sent_when_notifications_are_off()
    {
        await SaveSearch(notify: false);
        NewListing(publishedAt: Now.AddMinutes(1));

        Assert.Equal(0, await Notifier(Now.AddHours(1)).NotifyAsync());
        Assert.Empty(mail.Messages);
    }

    [Fact]
    public async Task Enabling_does_not_bring_the_whole_catalogue()
    {
        // Сотня оголошень існувала ДО того, як людина ввімкнула сповіщення.
        NewListing(publishedAt: Now.AddDays(-30));
        NewListing(publishedAt: Now.AddDays(-10));

        await SaveSearch(notify: true);

        Assert.Equal(0, await Notifier(Now.AddHours(1)).NotifyAsync());

        // Інакше перший же лист приніс би все, що підходить під фільтр за
        // весь час, — і людина відписалася б одразу.
        Assert.Empty(mail.Messages);
    }

    [Fact]
    public async Task Only_what_appeared_after_the_switch_counts()
    {
        NewListing(publishedAt: Now.AddDays(-1));

        var search = await SaveSearch(notify: true);

        NewListing(publishedAt: Now.AddMinutes(5));

        Assert.Equal(1, await Notifier(Now.AddHours(1)).NotifyAsync());

        var letter = Assert.Single(mail.Messages);
        Assert.Contains(search.Name, letter.Subject, StringComparison.Ordinal);
        Assert.Contains("1 нове авто", letter.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_same_car_is_not_reported_twice()
    {
        await SaveSearch(notify: true);
        NewListing(publishedAt: Now.AddMinutes(5));

        Assert.Equal(1, await Notifier(Now.AddHours(1)).NotifyAsync());

        var afterFirst = mail.Messages.Count;

        // Другий прохід через годину: нічого нового не з'явилося.
        Assert.Equal(0, await Notifier(Now.AddHours(2)).NotifyAsync());
        Assert.Equal(afterFirst, mail.Messages.Count);
    }

    [Fact]
    public async Task The_boundary_moves_even_when_nothing_was_found()
    {
        await SaveSearch(notify: true);

        await Notifier(Now.AddHours(1)).NotifyAsync();

        var search = await context.SavedSearches.AsNoTracking().SingleAsync();

        // Інакше кожен наступний запуск перебирав би все ширший проміжок.
        Assert.Equal(Now.AddHours(1), search.NotifyFrom);
    }

    [Fact]
    public async Task The_saved_filters_still_apply()
    {
        await SaveSearch(notify: true, new CatalogQuery { MakeId = 1 });

        NewListing(publishedAt: Now.AddMinutes(5), makeId: 2);

        // Нове авто є, але воно іншої марки — лист не потрібен.
        Assert.Equal(0, await Notifier(Now.AddHours(1)).NotifyAsync());
    }

    [Fact]
    public async Task A_draft_is_not_news()
    {
        await SaveSearch(notify: true);
        NewListing(publishedAt: Now.AddMinutes(5), status: ListingStatus.Draft);

        Assert.Equal(0, await Notifier(Now.AddHours(1)).NotifyAsync());
    }

    [Fact]
    public async Task An_unconfirmed_mailbox_gets_nothing()
    {
        var owner = await context.Users.SingleAsync(user => user.Id == OwnerId);
        owner.EmailConfirmed = false;
        await context.SaveChangesAsync();

        await SaveSearch(notify: true);
        NewListing(publishedAt: Now.AddMinutes(5));

        // Слати на непідтверджену скриньку означає годувати спам-фільтри.
        Assert.Equal(0, await Notifier(Now.AddHours(1)).NotifyAsync());
        Assert.Empty(mail.Messages);
    }

    [Fact]
    public async Task The_letter_lists_a_few_and_counts_the_rest()
    {
        await SaveSearch(notify: true);

        for (var i = 0; i < 8; i++)
        {
            NewListing(publishedAt: Now.AddMinutes(5 + i));
        }

        await Notifier(Now.AddHours(1)).NotifyAsync();

        var letter = Assert.Single(mail.Messages);

        Assert.Contains("8 нових авто", letter.Subject, StringComparison.Ordinal);

        // Лист із сорока картками ніхто не дочитає, тож решту згадуємо числом.
        Assert.Contains("ще 3", letter.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Turning_notifications_off_stops_the_letters()
    {
        var search = await SaveSearch(notify: true);

        await Service(Now).SetNotificationsAsync(search.Id, OwnerId, enabled: false);

        NewListing(publishedAt: Now.AddMinutes(5));

        Assert.Equal(0, await Notifier(Now.AddHours(1)).NotifyAsync());
    }

    [Fact]
    public async Task Turning_them_on_again_starts_from_that_moment()
    {
        var search = await SaveSearch(notify: false);

        NewListing(publishedAt: Now.AddMinutes(5));

        await Service(Now.AddHours(1)).SetNotificationsAsync(search.Id, OwnerId, enabled: true);

        // Авто з'явилося ДО повторного ввімкнення — воно вже не новина.
        Assert.Equal(0, await Notifier(Now.AddHours(2)).NotifyAsync());

        // А це вже після — і після минулого проходу розсилки.
        NewListing(publishedAt: Now.AddHours(2).AddMinutes(5));

        Assert.Equal(1, await Notifier(Now.AddHours(3)).NotifyAsync());
    }

    [Fact]
    public async Task One_broken_search_does_not_stop_the_rest()
    {
        await SaveSearch(notify: true, name: "Цілий");

        // Зіпсований JSON читається як порожній запит, тобто «будь-що».
        context.SavedSearches.Add(new Domain.Search.SavedSearch
        {
            UserId = OwnerId,
            Name = "Зіпсований",
            QueryJson = "{ це не json",
            CreatedAt = Now,
            NotifyByEmail = true,
            NotifyFrom = Now,
        });

        await context.SaveChangesAsync();

        NewListing(publishedAt: Now.AddMinutes(5));

        Assert.Equal(2, await Notifier(Now.AddHours(1)).NotifyAsync());
    }

    public void Dispose()
    {
        context.Dispose();
        database.Dispose();
        GC.SuppressFinalize(this);
    }

    private SavedSearchNotifier Notifier(DateTimeOffset now)
    {
        var options = Options.Create(new EmailOptions
        {
            SiteUrl = "http://localhost:5173",
            FromAddress = "no-reply@autolot.local",
        });

        return new SavedSearchNotifier(
            context,
            new FixedClock(now),
            Catalog(),
            mail,
            new SearchEmails(options),
            NullLogger<SavedSearchNotifier>.Instance);
    }

    private SavedSearchService Service(DateTimeOffset now) =>
        new(context, new FixedClock(now), Catalog());

    private CatalogService Catalog()
    {
        var language = new StubLanguage();

        var mapper = new ListingMapper(
            context,
            language,
            new StubCurrentUser(OwnerId),
            new GeoCatalog(context, language));

        return new CatalogService(context, new StubExchangeRates(), mapper);
    }

    private async Task<Application.Search.Dtos.SavedSearchCard> SaveSearch(
        bool notify,
        CatalogQuery? query = null,
        string name = "Мій пошук")
    {
        var saved = await Service(Now).SaveAsync(OwnerId, name, query ?? new CatalogQuery());

        if (notify)
        {
            await Service(Now).SetNotificationsAsync(saved.Id, OwnerId, enabled: true);
        }

        return saved;
    }

    private void NewListing(
        DateTimeOffset publishedAt,
        long makeId = 1,
        ListingStatus status = ListingStatus.Active)
    {
        context.Listings.Add(new Listing
        {
            Title = "Тестовий лот",
            Description = "Опис",
            SellerId = SellerId,
            CityId = 1,
            Price = 10_000m,
            Currency = Currency.Usd,
            PriceUah = 420_000m,
            Status = status,
            PublishedAt = publishedAt,
            Car = new Car
            {
                Year = 2020,
                MakeId = makeId,
                ModelId = makeId,
                FuelType = FuelType.Petrol,
                Transmission = TransmissionType.Manual,
                Drivetrain = DrivetrainType.FrontWheel,
                BodyType = BodyType.Sedan,
                Color = CarColor.Black,
            },
        });

        context.SaveChanges();
    }

    private void Seed()
    {
        context.Regions.Add(new Region { Id = 1, Code = "kyiv-region" });
        context.Cities.Add(new City { Id = 1, RegionId = 1, Code = "kyiv" });
        context.Makes.Add(new Make { Id = 1, Name = "BMW", Slug = "bmw" });
        context.Makes.Add(new Make { Id = 2, Name = "Audi", Slug = "audi" });
        context.Models.Add(new Model { Id = 1, MakeId = 1, Name = "X5", Slug = "x5" });
        context.Models.Add(new Model { Id = 2, MakeId = 2, Name = "A4", Slug = "a4" });

        context.Users.Add(new User
        {
            Id = OwnerId,
            UserName = "owner@example.com",
            Email = "owner@example.com",
            EmailConfirmed = true,
            DisplayName = "Власник",
        });

        context.Users.Add(new User
        {
            Id = SellerId,
            UserName = "seller@example.com",
            Email = "seller@example.com",
            DisplayName = "Продавець",
        });

        context.SaveChanges();
    }
}
