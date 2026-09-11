using AutoLot.Application.Bots;
using AutoLot.Domain.Bots;
using AutoLot.Domain.Identity;
using AutoLot.Infrastructure.Bots;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoLot.Tests.Bots;

/// <summary>
/// Прив'язка чату в месенджері до акаунта.
///
/// Тут перевіряється не стільки код, скільки правила довіри: код має діяти
/// один раз, недовго, і не давати доступу до чужого акаунта. Помилка в
/// будь-якому з цих трьох означає, що сторонній отримує сповіщення про чужі
/// ставки — а з часом і спосіб видати себе за господаря.
/// </summary>
public class BotLinkServiceTests : IDisposable
{
    private const long OwnerId = 1;
    private const long StrangerId = 2;

    private static readonly DateTimeOffset Now = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly TestDatabase database = new();

    private readonly AutoLotDbContext context;

    public BotLinkServiceTests()
    {
        context = database.CreateContext();
        Seed();
    }

    [Fact]
    public async Task A_fresh_code_links_the_chat()
    {
        var issued = await Service().IssueCodeAsync(OwnerId);

        var outcome = await Service().RedeemAsync(BotProvider.Telegram, "chat-1", "Олена", issued.Code);

        Assert.Equal(BotLinkOutcome.Linked, outcome);
        Assert.Equal(OwnerId, await Service().FindUserAsync(BotProvider.Telegram, "chat-1"));
    }

    [Fact]
    public async Task The_same_code_does_not_work_twice()
    {
        var issued = await Service().IssueCodeAsync(OwnerId);

        await Service().RedeemAsync(BotProvider.Telegram, "chat-1", null, issued.Code);

        // Другий чат тим самим кодом прив'язати не можна: інакше код,
        // підгляну­тий через плече, відкривав би доступ і далі.
        var second = await Service().RedeemAsync(BotProvider.Telegram, "chat-2", null, issued.Code);

        Assert.Equal(BotLinkOutcome.CodeRejected, second);
        Assert.Null(await Service().FindUserAsync(BotProvider.Telegram, "chat-2"));
    }

    [Fact]
    public async Task An_expired_code_is_refused()
    {
        var issued = await Service().IssueCodeAsync(OwnerId);

        // Через одинадцять хвилин код уже нічого не вартий.
        var later = ServiceAt(Now.Add(BotLinkCode.Lifetime).AddMinutes(1));

        Assert.Equal(
            BotLinkOutcome.CodeRejected,
            await later.RedeemAsync(BotProvider.Telegram, "chat-1", null, issued.Code));
    }

    [Fact]
    public async Task A_new_code_cancels_the_previous_one()
    {
        var first = await Service().IssueCodeAsync(OwnerId);
        await Service().IssueCodeAsync(OwnerId);

        // На екрані в людини тепер другий код, тож перший діяти не повинен —
        // інакше на один акаунт водночас існувало б кілька живих ключів.
        Assert.Equal(
            BotLinkOutcome.CodeRejected,
            await Service().RedeemAsync(BotProvider.Telegram, "chat-1", null, first.Code));
    }

    [Fact]
    public async Task An_invented_code_is_refused()
    {
        Assert.Equal(
            BotLinkOutcome.CodeRejected,
            await Service().RedeemAsync(BotProvider.Telegram, "chat-1", null, "000000"));
    }

    [Fact]
    public async Task Linking_the_same_chat_again_changes_nothing()
    {
        var first = await Service().IssueCodeAsync(OwnerId);
        await Service().RedeemAsync(BotProvider.Telegram, "chat-1", null, first.Code);

        var second = await Service().IssueCodeAsync(OwnerId);
        var outcome = await Service().RedeemAsync(BotProvider.Telegram, "chat-1", null, second.Code);

        Assert.Equal(BotLinkOutcome.AlreadyLinked, outcome);
        Assert.Single(await Service().GetRecipientsAsync(OwnerId));
    }

    [Fact]
    public async Task A_chat_handed_to_another_account_stops_serving_the_first()
    {
        var mine = await Service().IssueCodeAsync(OwnerId);
        await Service().RedeemAsync(BotProvider.Telegram, "chat-1", null, mine.Code);

        var theirs = await Service().IssueCodeAsync(StrangerId);
        await Service().RedeemAsync(BotProvider.Telegram, "chat-1", null, theirs.Code);

        // Двох господарів в одного чату бути не може: бот не знав би, кому
        // з них надсилати сповіщення.
        Assert.Equal(StrangerId, await Service().FindUserAsync(BotProvider.Telegram, "chat-1"));
        Assert.Empty(await Service().GetRecipientsAsync(OwnerId));
    }

    [Fact]
    public async Task The_same_chat_identifier_in_another_messenger_is_a_different_chat()
    {
        var issued = await Service().IssueCodeAsync(OwnerId);
        await Service().RedeemAsync(BotProvider.Telegram, "chat-1", null, issued.Code);

        // Номери чатів у Telegram і Viber незалежні, збіг нічого не означає.
        Assert.Null(await Service().FindUserAsync(BotProvider.Viber, "chat-1"));
    }

    [Fact]
    public async Task Unlinking_removes_the_chat()
    {
        var issued = await Service().IssueCodeAsync(OwnerId);
        await Service().RedeemAsync(BotProvider.Telegram, "chat-1", null, issued.Code);

        Assert.True(await Service().UnlinkAsync(BotProvider.Telegram, "chat-1"));
        Assert.Null(await Service().FindUserAsync(BotProvider.Telegram, "chat-1"));
    }

    [Fact]
    public async Task Unlinking_a_chat_that_was_never_linked_is_not_an_error()
    {
        // Людина могла надіслати /stop просто так — це не привід відповідати
        // помилкою.
        Assert.False(await Service().UnlinkAsync(BotProvider.Telegram, "chat-404"));
    }

    [Fact]
    public async Task The_code_is_six_digits()
    {
        var issued = await Service().IssueCodeAsync(OwnerId);

        Assert.Equal(BotLinkCode.Length, issued.Code.Length);
        Assert.True(issued.Code.All(char.IsAsciiDigit));
    }

    public void Dispose()
    {
        context.Dispose();
        database.Dispose();
        GC.SuppressFinalize(this);
    }

    private BotLinkService Service() => ServiceAt(Now);

    private BotLinkService ServiceAt(DateTimeOffset now) =>
        new(context, new FixedClock(now), NullLogger<BotLinkService>.Instance);

    private void Seed()
    {
        context.Users.AddRange(
            new User
            {
                Id = OwnerId,
                UserName = "owner@example.com",
                Email = "owner@example.com",
                DisplayName = "Господар",
            },
            new User
            {
                Id = StrangerId,
                UserName = "stranger@example.com",
                Email = "stranger@example.com",
                DisplayName = "Сторонній",
            });

        context.SaveChanges();
    }
}
