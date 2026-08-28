using AutoLot.Application.Listings;
using AutoLot.Domain.Cars;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Tests.TestDoubles;

namespace AutoLot.Tests.Listings;

/// <summary>
/// Правила доступу: хто має право питати, а хто — відповідати. Перевіряються
/// на базі, бо саме там лежить відповідь на питання «чий це лот».
/// </summary>
public class ListingQuestionServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    private const long SellerId = 1;

    private const long BuyerId = 2;

    private const long StrangerId = 3;

    private readonly TestDatabase database = new();

    private readonly AutoLotDbContext context;

    private readonly long listingId;

    public ListingQuestionServiceTests()
    {
        context = database.CreateContext();
        listingId = Seed();
    }

    [Fact]
    public async Task Anyone_can_ask_and_everyone_sees_it()
    {
        await Service().AskAsync(listingId, BuyerId, "Чи бита машина?");

        // Читає навіть той, хто не питав, — у цьому й сенс публічних питань.
        var questions = await Service().GetAsync(listingId);

        var question = Assert.Single(questions);
        Assert.Equal("Чи бита машина?", question.Text);
        Assert.Equal("Покупець", question.AskerName);
        Assert.Null(question.Answer);
    }

    [Fact]
    public async Task The_seller_cannot_ask_on_their_own_lot()
    {
        await Assert.ThrowsAsync<ListingAccessException>(
            () => Service().AskAsync(listingId, SellerId, "Сам себе питаю"));
    }

    [Fact]
    public async Task A_question_under_a_missing_lot_is_refused()
    {
        await Assert.ThrowsAsync<ListingNotFoundException>(
            () => Service().AskAsync(999_999, BuyerId, "Є хтось?"));
    }

    [Fact]
    public async Task A_question_under_a_draft_is_refused()
    {
        var draftId = Seed(ListingStatus.Draft);

        // Інакше за кодом відповіді можна було б намацати чужі чернетки.
        await Assert.ThrowsAsync<ListingNotFoundException>(
            () => Service().AskAsync(draftId, BuyerId, "Що це за лот?"));
    }

    [Fact]
    public async Task The_seller_answers_and_the_answer_becomes_public()
    {
        var asked = await Service().AskAsync(listingId, BuyerId, "Пробіг рідний?");

        var answered = await Service().AnswerAsync(asked.Id, SellerId, "Так, є книжка.");

        Assert.Equal("Так, є книжка.", answered.Answer);
        Assert.Equal(Now, answered.AnsweredAt);

        // Ім'я в записі лишається того, хто ПИТАВ, а не того, хто відповів.
        Assert.Equal("Покупець", answered.AskerName);
    }

    [Fact]
    public async Task A_stranger_cannot_answer()
    {
        var asked = await Service().AskAsync(listingId, BuyerId, "Скільки власників?");

        await Assert.ThrowsAsync<ListingAccessException>(
            () => Service().AnswerAsync(asked.Id, StrangerId, "Двоє, здається"));
    }

    [Fact]
    public async Task Even_the_asker_cannot_answer_their_own_question()
    {
        var asked = await Service().AskAsync(listingId, BuyerId, "Чи є гак?");

        await Assert.ThrowsAsync<ListingAccessException>(
            () => Service().AnswerAsync(asked.Id, BuyerId, "Сам відповім"));
    }

    [Fact]
    public async Task Answering_a_missing_question_is_refused()
    {
        await Assert.ThrowsAsync<QuestionNotFoundException>(
            () => Service().AnswerAsync(999_999, SellerId, "Відповідь у порожнечу"));
    }

    [Fact]
    public async Task The_newest_question_comes_first()
    {
        await ServiceAt(Now).AskAsync(listingId, BuyerId, "Питання перше");
        await ServiceAt(Now.AddMinutes(5)).AskAsync(listingId, StrangerId, "Питання друге");

        var questions = await Service().GetAsync(listingId);

        Assert.Equal("Питання друге", questions[0].Text);
        Assert.Equal("Питання перше", questions[1].Text);
    }

    public void Dispose()
    {
        context.Dispose();
        database.Dispose();
        GC.SuppressFinalize(this);
    }

    private ListingQuestionService Service() => ServiceAt(Now);

    private ListingQuestionService ServiceAt(DateTimeOffset now) =>
        new(context, new FixedClock(now), new ListingAccess(context));

    private long Seed(ListingStatus status = ListingStatus.Active)
    {
        if (context.Cities.Find(1L) is null)
        {
            context.Regions.Add(new Region { Id = 1, Code = "kyiv-region" });
            context.Cities.Add(new City { Id = 1, RegionId = 1, Code = "kyiv" });
            context.Makes.Add(new Make { Id = 1, Name = "BMW", Slug = "bmw" });
            context.Models.Add(new Model { Id = 1, MakeId = 1, Name = "X5", Slug = "x5" });

            context.Users.Add(NewUser(SellerId, "seller", "Продавець"));
            context.Users.Add(NewUser(BuyerId, "buyer", "Покупець"));
            context.Users.Add(NewUser(StrangerId, "stranger", "Перехожий"));

            context.SaveChanges();
        }

        var listing = new Listing
        {
            Title = "Тестовий лот",
            Description = "Опис",
            SellerId = SellerId,
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

    private static User NewUser(long id, string login, string displayName) => new()
    {
        Id = id,
        UserName = $"{login}@example.com",
        Email = $"{login}@example.com",
        DisplayName = displayName,
    };
}
