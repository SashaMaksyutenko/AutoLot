using AutoLot.Application.Chat;
using AutoLot.Application.Chat.Dtos;
using AutoLot.Application.Listings;
using AutoLot.Domain.Cars;
using AutoLot.Domain.Dealers;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Chat;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Tests.Chat;

/// <summary>
/// Приватне листування. Головне тут — межа доступу: сторонній не має бачити
/// чужу переписку навіть на читання, а менеджер салону навпаки має відповідати
/// на листи, адресовані салону.
/// </summary>
public class ChatServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    private const long SellerId = 1;

    private const long BuyerId = 2;

    private const long StrangerId = 3;

    private const long ColleagueId = 4;

    private const long DealershipId = 1;

    private readonly TestDatabase database = new();

    private readonly AutoLotDbContext context;

    private readonly RecordingChatNotifier notifier = new();

    private readonly long listingId;

    private readonly long salonListingId;

    public ChatServiceTests()
    {
        context = database.CreateContext();
        Seed();

        listingId = NewListing(SellerId, dealershipId: null);
        salonListingId = NewListing(SellerId, DealershipId);
    }

    [Fact]
    public async Task A_buyer_starts_a_conversation()
    {
        var conversation = await Service().StartAsync(listingId, BuyerId);

        Assert.Equal(listingId, conversation.ListingId);
        Assert.False(conversation.ViewerIsSeller);
        Assert.Empty(conversation.Messages);
    }

    [Fact]
    public async Task Starting_twice_returns_the_same_thread()
    {
        var first = await Service().StartAsync(listingId, BuyerId);
        var second = await Service().StartAsync(listingId, BuyerId);

        // Інакше кожне натискання «написати» починало б нову гілку, і
        // листування розсипалося б на десяток однакових.
        Assert.Equal(first.Id, second.Id);
        Assert.Single(context.Conversations);
    }

    [Fact]
    public async Task The_seller_cannot_write_to_themselves()
    {
        await Assert.ThrowsAsync<ChatNotAllowedException>(
            () => Service().StartAsync(listingId, SellerId));
    }

    [Fact]
    public async Task A_manager_cannot_write_to_their_own_salon()
    {
        await Assert.ThrowsAsync<ChatNotAllowedException>(
            () => Service().StartAsync(salonListingId, ColleagueId));
    }

    [Fact]
    public async Task A_conversation_under_a_draft_is_refused()
    {
        var draftId = NewListing(SellerId, dealershipId: null, ListingStatus.Draft);

        await Assert.ThrowsAsync<ListingNotFoundException>(
            () => Service().StartAsync(draftId, BuyerId));
    }

    [Fact]
    public async Task Both_sides_see_the_same_thread()
    {
        var conversation = await Service().StartAsync(listingId, BuyerId);

        await Service().SendAsync(conversation.Id, BuyerId, "Доброго дня, ще актуально?");
        await Service().SendAsync(conversation.Id, SellerId, "Так, продаю.");

        var asSeller = await Service().GetAsync(conversation.Id, SellerId);

        Assert.True(asSeller.ViewerIsSeller);
        Assert.Equal(2, asSeller.Messages.Count);
        Assert.Equal("Покупець", asSeller.CompanionName);
    }

    [Fact]
    public async Task A_stranger_gets_nothing_and_is_told_it_does_not_exist()
    {
        var conversation = await Service().StartAsync(listingId, BuyerId);

        // Саме «не знайдено», а не «немає доступу»: існування чужого
        // листування не справа стороннього.
        await Assert.ThrowsAsync<ConversationNotFoundException>(
            () => Service().GetAsync(conversation.Id, StrangerId));

        await Assert.ThrowsAsync<ConversationNotFoundException>(
            () => Service().SendAsync(conversation.Id, StrangerId, "Підслухаю"));
    }

    [Fact]
    public async Task A_colleague_answers_a_letter_addressed_to_the_salon()
    {
        var conversation = await Service().StartAsync(salonListingId, BuyerId);
        await Service().SendAsync(conversation.Id, BuyerId, "Чи є розстрочка?");

        // Заради цього правило доступу й спільне: менеджер поїхав, лист має
        // кому відповісти.
        var answered = await Service().SendAsync(conversation.Id, ColleagueId, "Так, є.");

        Assert.Equal(ColleagueId, answered.SenderId);
    }

    [Fact]
    public async Task Opening_a_thread_marks_the_other_side_as_read()
    {
        var conversation = await Service().StartAsync(listingId, BuyerId);
        await Service().SendAsync(conversation.Id, BuyerId, "Питання");

        Assert.Equal(1, await Service().GetUnreadCountAsync(SellerId));

        await Service().GetAsync(conversation.Id, SellerId);

        // Відкрити розмову й означає прочитати її.
        Assert.Equal(0, await Service().GetUnreadCountAsync(SellerId));
    }

    [Fact]
    public async Task Your_own_messages_never_count_as_unread()
    {
        var conversation = await Service().StartAsync(listingId, BuyerId);
        await Service().SendAsync(conversation.Id, BuyerId, "Питання");

        Assert.Equal(0, await Service().GetUnreadCountAsync(BuyerId));
    }

    [Fact]
    public async Task The_read_time_is_when_it_was_first_seen()
    {
        var conversation = await Service().StartAsync(listingId, BuyerId);
        await Service().SendAsync(conversation.Id, BuyerId, "Питання");

        await ServiceAt(Now.AddMinutes(5)).GetAsync(conversation.Id, SellerId);
        await ServiceAt(Now.AddMinutes(30)).GetAsync(conversation.Id, SellerId);

        // AsNoTracking обов'язковий: позначення прочитаним іде через
        // ExecuteUpdate, тобто прямо в базу, оминаючи стеження EF. Відстежувана
        // копія в пам'яті лишилася б зі старим порожнім значенням.
        var message = context.Messages.AsNoTracking().Single();

        // Повторне відкриття не має пересувати час: важливо, коли побачили
        // ВПЕРШЕ.
        Assert.Equal(Now.AddMinutes(5), message.ReadAt);
    }

    [Fact]
    public async Task The_list_is_sorted_by_the_latest_message()
    {
        var first = await Service().StartAsync(listingId, BuyerId);
        var second = await Service().StartAsync(salonListingId, BuyerId);

        await ServiceAt(Now.AddMinutes(1)).SendAsync(first.Id, BuyerId, "Перше");
        await ServiceAt(Now.AddMinutes(2)).SendAsync(second.Id, BuyerId, "Друге");

        var mine = await Service().GetMineAsync(BuyerId);

        Assert.Equal(second.Id, mine[0].Id);
        Assert.Equal("Друге", mine[0].LastMessageText);
    }

    [Fact]
    public async Task The_seller_side_is_notified_when_the_buyer_writes()
    {
        var conversation = await Service().StartAsync(salonListingId, BuyerId);

        await Service().SendAsync(conversation.Id, BuyerId, "Питання");

        // Для салонного лота повідомити треба весь персонал, а не лише того,
        // хто подав оголошення.
        var sent = Assert.Single(notifier.Sent);
        Assert.Contains(SellerId, sent.Recipients);
        Assert.Contains(ColleagueId, sent.Recipients);
    }

    [Fact]
    public async Task The_buyer_is_notified_when_the_seller_answers()
    {
        var conversation = await Service().StartAsync(listingId, BuyerId);
        await Service().SendAsync(conversation.Id, SellerId, "Відповідь");

        var sent = Assert.Single(notifier.Sent);
        Assert.Equal([BuyerId], sent.Recipients);
    }

    public void Dispose()
    {
        context.Dispose();
        database.Dispose();
        GC.SuppressFinalize(this);
    }

    private ChatService Service() => ServiceAt(Now);

    private ChatService ServiceAt(DateTimeOffset now) =>
        new(context, new FixedClock(now), new ListingAccess(context), notifier);

    private long NewListing(
        long sellerId,
        long? dealershipId,
        ListingStatus status = ListingStatus.Active)
    {
        var listing = new Listing
        {
            Title = "Тестове авто",
            Description = "Опис",
            SellerId = sellerId,
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
        context.Users.Add(NewUser(StrangerId, "stranger", "Сторонній"));
        context.Users.Add(NewUser(ColleagueId, "colleague", "Колега"));

        context.Dealerships.Add(new Dealership
        {
            Id = DealershipId,
            Name = "Авто Плюс",
            Slug = "avto-plyus",
            CityId = 1,
        });

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

    private static User NewUser(long id, string login, string displayName) => new()
    {
        Id = id,
        UserName = $"{login}@example.com",
        Email = $"{login}@example.com",
        NormalizedEmail = $"{login}@example.com".ToUpperInvariant(),
        DisplayName = displayName,
    };
}

/// <summary>Записує, кому й що пішло б, замість справжньої розсилки.</summary>
internal sealed class RecordingChatNotifier : IChatNotifier
{
    private readonly List<(IReadOnlyList<long> Recipients, MessageRecord Message)> sent = [];

    public IReadOnlyList<(IReadOnlyList<long> Recipients, MessageRecord Message)> Sent => sent;

    public Task MessageSentAsync(
        IReadOnlyList<long> recipientIds,
        MessageRecord message,
        CancellationToken cancellationToken = default)
    {
        sent.Add((recipientIds, message));

        return Task.CompletedTask;
    }
}
