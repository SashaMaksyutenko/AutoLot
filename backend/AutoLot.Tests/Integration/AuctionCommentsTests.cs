using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoLot.Application.Auctions.Dtos;
using Microsoft.AspNetCore.SignalR.Client;
using Npgsql;

namespace AutoLot.Tests.Integration;

/// <summary>
/// Живі коментарі під лотом.
///
/// Головне тут — слово «живі». Тому є тест, який підключається до хаба тим
/// самим способом, що й браузер, і перевіряє, що коментар справді ДОХОДИТЬ
/// до глядача, а не лише що сервіс «викликав розсилку»: помилка в назві
/// події чи групи інакше пройшла б усі перевірки й тихо зламала б сторінку.
/// </summary>
[Collection(ApiGroup.Name)]
public class AuctionCommentsTests(ApiFixture api)
{
    [Fact]
    public async Task A_comment_appears_under_the_lot()
    {
        var (lot, _) = await LiveLotAsync();
        var viewer = await api.SellerAsync();

        await PostAsync(lot, viewer, "На третьому фото видно іржу на порозі.", HttpStatusCode.OK);

        var comments = await CommentsAsync(lot);

        Assert.Equal("На третьому фото видно іржу на порозі.", comments[0].GetProperty("text").GetString());
        Assert.False(comments[0].GetProperty("isSeller").GetBoolean());
    }

    /// <summary>
    /// Відповідь продавця позначається окремо: вона важить більше за чужу
    /// думку, і загубитися серед інших не має.
    /// </summary>
    [Fact]
    public async Task The_sellers_own_comment_is_marked()
    {
        var (lot, seller) = await LiveLotAsync();

        await PostAsync(lot, seller, "Пороги варені в 2022-му, чеки є.", HttpStatusCode.OK);

        Assert.True((await CommentsAsync(lot))[0].GetProperty("isSeller").GetBoolean());
    }

    [Fact]
    public async Task Newer_comments_come_first()
    {
        var (lot, seller) = await LiveLotAsync();
        var viewer = await api.SellerAsync();

        await PostAsync(lot, viewer, "Перший.", HttpStatusCode.OK);
        await PostAsync(lot, seller, "Другий.", HttpStatusCode.OK);

        var texts = (await CommentsAsync(lot))
            .EnumerateArray()
            .Select(comment => comment.GetProperty("text").GetString());

        Assert.Equal(["Другий.", "Перший."], texts);
    }

    [Fact]
    public async Task A_guest_reads_but_cannot_write()
    {
        var (lot, seller) = await LiveLotAsync();

        await PostAsync(lot, seller, "Для всіх.", HttpStatusCode.OK);

        Assert.NotEqual(0, (await CommentsAsync(lot)).GetArrayLength());

        using var anonymous = await api.Client.PostAsJsonAsync(
            new Uri($"/api/listings/{lot}/auction/comments", UriKind.Relative),
            new { text = "Можна без входу?" });

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    /// <summary>
    /// Коментарі — частина торгів. Під оголошенням із фіксованою ціною їх
    /// немає: там для запитань є звичайні питання продавцю.
    /// </summary>
    [Fact]
    public async Task A_fixed_price_listing_has_no_comments()
    {
        var seller = await api.SellerAsync();
        var listingId = await api.DraftAsync(seller);

        await api.PublishAsync(listingId, seller);

        await PostAsync(listingId, seller, "Тут не можна.", HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_comment_is_refused(string text)
    {
        var (lot, seller) = await LiveLotAsync();

        await PostAsync(lot, seller, text, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_wall_of_text_is_refused()
    {
        var (lot, seller) = await LiveLotAsync();

        await PostAsync(lot, seller, new string('а', 1001), HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Жива стрічка без жодного обмеження — запрошення засипати її однаковими
    /// рядками. Пауза в кілька секунд нормальній розмові не заважає.
    /// </summary>
    [Fact]
    public async Task Writing_too_fast_is_slowed_down()
    {
        var (lot, seller) = await LiveLotAsync();

        await PostAsync(lot, seller, "Раз.", HttpStatusCode.OK);
        await PostAsync(lot, seller, "Два.", HttpStatusCode.Conflict);
    }

    /// <summary>
    /// Після фіналу коментувати не можна: розмова перетворилася б на
    /// обговорення результату. Читати — можна й далі.
    /// </summary>
    [Fact]
    public async Task After_the_hammer_the_comments_close()
    {
        var (lot, seller) = await LiveLotAsync();

        await PostAsync(lot, seller, "Поки торги йдуть.", HttpStatusCode.OK);
        await EndAuctionAsync(lot);

        var viewer = await api.SellerAsync();

        await PostAsync(lot, viewer, "Запізнився.", HttpStatusCode.Conflict);
    }

    /// <summary>
    /// Найважливіший тест: глядач, підключений до лота, отримує коментар у
    /// живу — тієї ж миті, без оновлення сторінки.
    /// </summary>
    [Fact]
    public async Task A_watcher_receives_the_comment_live()
    {
        var (lot, _) = await LiveLotAsync();
        var author = await api.SellerAsync();

        await using var hub = api.AuctionHub();

        // TaskCompletionSource — «обіцянка, яку ми виконаємо самі»: тест
        // чекатиме на неї, а виконає її обробник, щойно прийде подія.
        var received = new TaskCompletionSource<CommentRecord>(TaskCreationOptions.RunContinuationsAsynchronously);

        hub.On<CommentRecord>("commentPosted", comment => received.TrySetResult(comment));

        await hub.StartAsync();
        await hub.InvokeAsync("Watch", lot);

        await PostAsync(lot, author, "Бачу в живу!", HttpStatusCode.OK);

        var comment = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(lot, comment.ListingId);
        Assert.Equal("Бачу в живу!", comment.Text);
    }

    /// <summary>
    /// А глядач ІНШОГО лота цієї новини не отримує: у кожного лота своя
    /// група, і розмова під одним авто не має сипатися на сторінку іншого.
    /// </summary>
    [Fact]
    public async Task A_watcher_of_another_lot_hears_nothing()
    {
        var (lot, _) = await LiveLotAsync();
        var (otherLot, _) = await LiveLotAsync();
        var author = await api.SellerAsync();

        await using var hub = api.AuctionHub();

        var received = new TaskCompletionSource<CommentRecord>(TaskCreationOptions.RunContinuationsAsynchronously);

        hub.On<CommentRecord>("commentPosted", comment => received.TrySetResult(comment));

        await hub.StartAsync();
        await hub.InvokeAsync("Watch", otherLot);

        await PostAsync(lot, author, "Це не вам.", HttpStatusCode.OK);

        // Даємо розсилці час дійти, якби вона пішла не туди.
        var winner = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.NotSame(received.Task, winner);
    }

    // ─────────────────────────── Оснащення ───────────────────────────

    /// <summary>Лот із торгами, що вже йдуть, і токен його продавця.</summary>
    private async Task<(long Lot, string Seller)> LiveLotAsync()
    {
        var seller = await api.SellerAsync();
        var lot = await api.DraftAsync(seller, auction: true);

        await api.PublishAsync(lot, seller);

        return (lot, seller);
    }

    private async Task PostAsync(long lot, string token, string text, HttpStatusCode expected)
    {
        using var response = await api.SendAsync(
            HttpMethod.Post,
            $"/api/listings/{lot}/auction/comments",
            token,
            new { text });

        Assert.Equal(expected, response.StatusCode);
    }

    private async Task<JsonElement> CommentsAsync(long lot) =>
        await api.Client.GetFromJsonAsync<JsonElement>(
            new Uri($"/api/listings/{lot}/auction/comments", UriKind.Relative));

    /// <summary>
    /// Закриває торги прямо в базі. Чекати справжнього фіналу тест не може —
    /// найкоротші торги тривають години.
    /// </summary>
    private async Task EndAuctionAsync(long lot)
    {
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "UPDATE auctions SET status = 'Ended' WHERE listing_id = @lot",
            connection);

        command.Parameters.AddWithValue("lot", lot);
        await command.ExecuteNonQueryAsync();
    }
}
