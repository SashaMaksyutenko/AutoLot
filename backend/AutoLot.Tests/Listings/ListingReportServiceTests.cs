using AutoLot.Application.Listings;
using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Cars;
using AutoLot.Domain.Common;
using AutoLot.Domain.Dealers;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoLot.Tests.Listings;

/// <summary>
/// Скарги на оголошення. Головне тут — що робить схвалена скарга: вона має
/// не лише змінити свій стан, а й зняти оголошення й прибрати з черги решту
/// скарг на нього.
/// </summary>
public class ListingReportServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    private const long SellerId = 1;

    private const long BuyerId = 2;

    private const long StrangerId = 3;

    private const long ColleagueId = 4;

    private const long ModeratorId = 5;

    private const long DealershipId = 1;

    private readonly TestDatabase database = new();

    private readonly AutoLotDbContext context;

    private readonly long listingId;

    private readonly long salonListingId;

    public ListingReportServiceTests()
    {
        context = database.CreateContext();
        Seed();

        listingId = NewListing(dealershipId: null);
        salonListingId = NewListing(DealershipId);
    }

    [Fact]
    public async Task A_visitor_reports_a_listing()
    {
        var receipt = await Service().SubmitAsync(listingId, BuyerId, Report(ListingReportReason.Fraud));

        Assert.True(receipt.IsNew);
        Assert.Equal(listingId, receipt.ListingId);
        Assert.Equal(ListingReportReason.Fraud, receipt.Reason);
        Assert.Equal(Now, receipt.CreatedAt);
    }

    [Fact]
    public async Task Reporting_twice_does_not_double_the_queue()
    {
        var first = await Service().SubmitAsync(listingId, BuyerId, Report(ListingReportReason.Fraud));
        var second = await Service().SubmitAsync(listingId, BuyerId, Report(ListingReportReason.Duplicate));

        // Друга скарга тієї самої людини нічого не додає — повертається перша.
        Assert.Equal(first.Id, second.Id);
        Assert.False(second.IsNew);
        Assert.Equal(ListingReportReason.Fraud, second.Reason);
        Assert.Single(context.ListingReports);
    }

    [Fact]
    public async Task Different_people_each_get_their_own_report()
    {
        await Service().SubmitAsync(listingId, BuyerId, Report(ListingReportReason.Fraud));
        await Service().SubmitAsync(listingId, StrangerId, Report(ListingReportReason.AlreadySold));

        Assert.Equal(2, await context.ListingReports.CountAsync());
    }

    [Fact]
    public async Task The_author_cannot_report_their_own_listing()
    {
        await Assert.ThrowsAsync<ReportNotAllowedException>(
            () => Service().SubmitAsync(listingId, SellerId, Report(ListingReportReason.Fraud)));
    }

    [Fact]
    public async Task A_manager_cannot_report_their_own_salon()
    {
        // Правило власності те саме, що й для решти дій з оголошенням.
        await Assert.ThrowsAsync<ReportNotAllowedException>(
            () => Service().SubmitAsync(salonListingId, ColleagueId, Report(ListingReportReason.Fraud)));
    }

    [Fact]
    public async Task A_draft_cannot_be_reported()
    {
        var draftId = NewListing(dealershipId: null, ListingStatus.Draft);

        // Чернетки ніхто не бачить, тож і шкоди від неї немає. Відповідь саме
        // «не знайдено»: за іншим кодом можна було б намацати чужі чернетки.
        await Assert.ThrowsAsync<ListingNotFoundException>(
            () => Service().SubmitAsync(draftId, BuyerId, Report(ListingReportReason.Fraud)));
    }

    [Fact]
    public async Task The_queue_shows_the_oldest_first_with_the_reason_in_words()
    {
        await ServiceAt(Now.AddMinutes(5)).SubmitAsync(listingId, StrangerId, Report(ListingReportReason.Duplicate));
        await ServiceAt(Now).SubmitAsync(listingId, BuyerId, Report(ListingReportReason.Fraud));

        var queue = await Service().GetQueueAsync();

        Assert.Equal(2, queue.Count);
        Assert.Equal(ListingReportReason.Fraud, queue[0].Reason);
        Assert.Equal("Схоже на шахрайство", queue[0].ReasonName);
        Assert.Equal("Покупець", queue[0].ReporterName);
        Assert.Equal("Тестовий лот", queue[0].ListingTitle);
    }

    [Fact]
    public async Task The_queue_says_how_many_more_complaints_that_listing_has()
    {
        await Service().SubmitAsync(listingId, BuyerId, Report(ListingReportReason.Fraud));
        await Service().SubmitAsync(listingId, StrangerId, Report(ListingReportReason.Fraud));
        await Service().SubmitAsync(salonListingId, BuyerId, Report(ListingReportReason.Offensive));

        var queue = await Service().GetQueueAsync();

        // Дві скарги на один лот бачать одна одну; скарга на інший лот
        // до них не додається.
        Assert.Equal(1, queue.Single(item => item.ListingId == listingId && item.ReporterName == "Покупець").OtherPendingForListing);
        Assert.Equal(0, queue.Single(item => item.ListingId == salonListingId).OtherPendingForListing);
    }

    [Fact]
    public async Task An_upheld_complaint_takes_the_listing_down()
    {
        var receipt = await Service().SubmitAsync(listingId, BuyerId, Report(ListingReportReason.WrongInformation));

        await Service().ResolveAsync(receipt.Id, ModeratorId, Resolve(accepted: true, "Пробіг скручений"));

        var listing = await context.Listings.AsNoTracking().SingleAsync(item => item.Id == listingId);

        Assert.Equal(ListingStatus.Rejected, listing.Status);

        // Автор бачить причину словами, без імені скаржника й без його
        // коментаря — той писався модератору.
        Assert.Equal(
            "Знято з публікації за скаргою: Недостовірні дані про авто.",
            listing.RejectionReason);
    }

    [Fact]
    public async Task An_upheld_complaint_closes_the_others_about_the_same_listing()
    {
        var mine = await Service().SubmitAsync(listingId, BuyerId, Report(ListingReportReason.Fraud));
        await Service().SubmitAsync(listingId, StrangerId, Report(ListingReportReason.Fraud));

        await Service().ResolveAsync(mine.Id, ModeratorId, Resolve(accepted: true, note: null));

        // Інакше модератор, знявши лот, отримав би другу скаргу про вже зняте.
        Assert.Empty(await Service().GetQueueAsync());
        Assert.All(
            await context.ListingReports.AsNoTracking().ToListAsync(),
            report => Assert.Equal(ListingReportStatus.Accepted, report.Status));
    }

    [Fact]
    public async Task A_dismissed_complaint_leaves_the_listing_and_the_others_alone()
    {
        var mine = await Service().SubmitAsync(listingId, BuyerId, Report(ListingReportReason.Fraud));
        await Service().SubmitAsync(listingId, StrangerId, Report(ListingReportReason.Offensive));

        await Service().ResolveAsync(mine.Id, ModeratorId, Resolve(accepted: false, "Наклеп"));

        var listing = await context.Listings.AsNoTracking().SingleAsync(item => item.Id == listingId);

        Assert.Equal(ListingStatus.Active, listing.Status);

        // Друга скарга могла бути про інше й має право на власний розгляд.
        var remaining = Assert.Single(await Service().GetQueueAsync());
        Assert.Equal(ListingReportReason.Offensive, remaining.Reason);
    }

    [Fact]
    public async Task Upholding_a_complaint_about_a_withdrawn_listing_changes_nothing_but_the_report()
    {
        var receipt = await Service().SubmitAsync(listingId, BuyerId, Report(ListingReportReason.Fraud));

        // Поки скарга чекала, автор сам прибрав оголошення.
        var listing = await context.Listings.SingleAsync(item => item.Id == listingId);
        listing.Archive();
        await context.SaveChangesAsync();

        await Service().ResolveAsync(receipt.Id, ModeratorId, Resolve(accepted: true, note: null));

        // Рішення записане, а знімати вже нічого — падати тут не за чим.
        var stored = await context.Listings.AsNoTracking().SingleAsync(item => item.Id == listingId);
        Assert.Equal(ListingStatus.Archived, stored.Status);
        Assert.Equal(
            ListingReportStatus.Accepted,
            (await context.ListingReports.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Resolving_the_same_complaint_twice_is_refused()
    {
        var receipt = await Service().SubmitAsync(listingId, BuyerId, Report(ListingReportReason.Fraud));

        await Service().ResolveAsync(receipt.Id, ModeratorId, Resolve(accepted: false, note: null));

        await Assert.ThrowsAsync<DomainRuleException>(
            () => Service().ResolveAsync(receipt.Id, ModeratorId, Resolve(accepted: true, note: null)));
    }

    [Fact]
    public async Task Resolving_a_missing_complaint_is_refused()
    {
        await Assert.ThrowsAsync<ReportNotFoundException>(
            () => Service().ResolveAsync(999_999, ModeratorId, Resolve(accepted: true, note: null)));
    }

    [Fact]
    public async Task After_a_verdict_the_same_person_may_report_again()
    {
        var first = await Service().SubmitAsync(listingId, BuyerId, Report(ListingReportReason.Fraud));
        await Service().ResolveAsync(first.Id, ModeratorId, Resolve(accepted: false, note: null));

        var second = await Service().SubmitAsync(listingId, BuyerId, Report(ListingReportReason.AlreadySold));

        // Саме тому пара «лот + скаржник» не унікальна в базі: оголошення
        // могло змінитися після першого розгляду.
        Assert.True(second.IsNew);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task The_reasons_come_with_names_in_the_reader_language()
    {
        var reasons = await Service().GetReasonsAsync();

        Assert.Equal(6, reasons.Count);

        // Порядок береться з сід-файла, а не з алфавіту: найчастіше
        // обирають перший пункт, а «інше» має лишатися останнім.
        Assert.Equal("Fraud", reasons[0].Value);
        Assert.Equal("Other", reasons[^1].Value);
    }

    public void Dispose()
    {
        context.Dispose();
        database.Dispose();
        GC.SuppressFinalize(this);
    }

    private static SubmitReportRequest Report(ListingReportReason reason) =>
        new() { Reason = reason, Comment = "Пояснення" };

    private static ResolveReportRequest Resolve(bool accepted, string? note) =>
        new() { Accepted = accepted, Note = note };

    private ListingReportService Service() => ServiceAt(Now);

    private ListingReportService ServiceAt(DateTimeOffset now) =>
        new(
            context,
            new FixedClock(now),
            new StubLanguage(),
            new ListingAccess(context),
            NullLogger<ListingReportService>.Instance);

    private long NewListing(long? dealershipId, ListingStatus status = ListingStatus.Active)
    {
        var listing = new Listing
        {
            Title = "Тестовий лот",
            Description = "Опис",
            SellerId = SellerId,
            DealershipId = dealershipId,
            CityId = 1,
            Price = 10_000m,
            Currency = Currency.Usd,
            PriceUah = 420_000m,
            Status = status,
            Car = new Car
            {
                Year = 2020,
                MakeId = 1,
                ModelId = 1,
                FuelType = FuelType.Petrol,
                Transmission = TransmissionType.Manual,
                Drivetrain = DrivetrainType.FrontWheel,
                BodyType = BodyType.Sedan,
                Color = CarColor.Black,
            },
        };

        context.Listings.Add(listing);
        context.SaveChanges();

        return listing.Id;
    }

    private void Seed()
    {
        context.Regions.Add(new Region { Id = 1, Code = "kyiv-region" });
        context.Cities.Add(new City { Id = 1, RegionId = 1, Code = "kyiv" });
        context.Makes.Add(new Make { Id = 1, Name = "BMW", Slug = "bmw" });
        context.Models.Add(new Model { Id = 1, MakeId = 1, Name = "X5", Slug = "x5" });

        context.Users.Add(NewUser(SellerId, "seller", "Продавець"));
        context.Users.Add(NewUser(BuyerId, "buyer", "Покупець"));
        context.Users.Add(NewUser(StrangerId, "stranger", "Перехожий"));
        context.Users.Add(NewUser(ColleagueId, "colleague", "Колега"));
        context.Users.Add(NewUser(ModeratorId, "moderator", "Модератор"));

        context.Dealerships.Add(new Dealership
        {
            Id = DealershipId,
            Name = "Авто Плюс",
            Slug = "avto-plyus",
            CityId = 1,
        });

        SeedReasons();

        context.SaveChanges();

        context.DealershipMembers.Add(new DealershipMember
        {
            DealershipId = DealershipId,
            UserId = SellerId,
            Role = DealershipRole.Owner,
        });

        context.DealershipMembers.Add(new DealershipMember
        {
            DealershipId = DealershipId,
            UserId = ColleagueId,
            Role = DealershipRole.Manager,
        });

        context.SaveChanges();
    }

    /// <summary>
    /// Назви причин у тестовій базі. Беремо ті самі, що й у сід-файлі: тест
    /// на порядок і на переклад інакше перевіряв би сам себе.
    /// </summary>
    private void SeedReasons()
    {
        string[] names =
        [
            "Схоже на шахрайство",
            "Недостовірні дані про авто",
            "Авто вже продано",
            "Повтор іншого оголошення",
            "Образливий вміст або реклама",
            "Інше",
        ];

        var values = Enum.GetValues<ListingReportReason>();

        for (var index = 0; index < values.Length; index++)
        {
            context.EnumTranslations.Add(new EnumTranslation
            {
                EnumName = nameof(ListingReportReason),
                Value = values[index].ToString(),
                Language = LanguageCodes.Ukrainian,
                Name = names[index],
                SortOrder = index,
            });
        }
    }

    private static User NewUser(long id, string login, string displayName) => new()
    {
        Id = id,
        UserName = $"{login}@example.com",
        Email = $"{login}@example.com",
        DisplayName = displayName,
    };
}
