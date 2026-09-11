using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace AutoLot.Infrastructure.Bots;

/// <summary>Одне повідомлення, яке надіслала людина.</summary>
internal sealed record TelegramMessage(long UpdateId, string ChatId, string? ChatName, string Text);

/// <summary>
/// Тонка обгортка над HTTP API Telegram.
///
/// Готової бібліотеки тут немає навмисно: з усього API потрібні рівно два
/// методи — забрати оновлення й надіслати текст. Пакет заради двох запитів
/// додав би залежність, яку довелося б оновлювати роками.
/// </summary>
internal sealed class TelegramClient(HttpClient http, IOptions<TelegramOptions> options)
{
    private readonly TelegramOptions settings = options.Value;

    /*
      Усі шляхи починаються зі скісної риски, і це не косметика.

      Токен має всередині двокрапку, тож рядок «bot123:ABC/getUpdates» без
      неї читається як адреса зі схемою «bot123» — і запит падає ще до
      того, як кудись піти. Провідна риска однозначно каже: це шлях.
    */

    /// <summary>
    /// Забирає нові повідомлення.
    ///
    /// <paramref name="offset"/> — підтвердження: надіславши його, ми кажемо
    /// серверу, що все до цього номера отримали, і він більше цього не
    /// повторить. Без підтвердження ті самі повідомлення приходили б по колу.
    /// </summary>
    public async Task<IReadOnlyList<TelegramMessage>> GetUpdatesAsync(
        long offset,
        CancellationToken cancellationToken)
    {
        var url =
            $"/bot{settings.Token}/getUpdates"
            + $"?timeout={settings.PollTimeoutSeconds}"
            + $"&allowed_updates=[\"message\"]"
            + (offset > 0 ? $"&offset={offset}" : string.Empty);

        var response = await http.GetFromJsonAsync<UpdatesResponse>(url, cancellationToken);

        if (response is not { Ok: true, Result: not null })
        {
            return [];
        }

        return [.. response.Result
            .Where(update => update.Message?.Chat is not null && update.Message.Text is not null)
            .Select(update => new TelegramMessage(
                update.UpdateId,
                update.Message!.Chat!.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                update.Message.Chat.FirstName ?? update.Message.Chat.Username,
                update.Message.Text!))];
    }

    /// <summary>
    /// Як звуть самого бота. Питаємо в Telegram, а не тримаємо в
    /// налаштуваннях: ім'я задають у @BotFather, і дублювати його руками
    /// означало б рано чи пізно розійтися з дійсністю.
    /// </summary>
    public async Task<string?> GetUsernameAsync(CancellationToken cancellationToken)
    {
        var response = await http.GetFromJsonAsync<MeResponse>(
            $"/bot{settings.Token}/getMe",
            cancellationToken);

        return response is { Ok: true, Result: not null } ? response.Result.Username : null;
    }

    public async Task SendAsync(string chatId, string text, CancellationToken cancellationToken)
    {
        var payload = new
        {
            chat_id = chatId,
            text,

            // Розмітка вимкнена навмисно: у текстах трапляються назви авто на
            // кшталт «BMW X5 (E70) 3.0*», і будь-який символ розмітки в них
            // перетворив би повідомлення на помилку замість тексту.
            disable_web_page_preview = true,
        };

        using var response = await http.PostAsJsonAsync(
            $"/bot{settings.Token}/sendMessage",
            payload,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    // ── Форма відповіді Telegram ─────────────────────────────────────
    //
    // Описуємо лише ті поля, які справді читаємо: решту серіалізатор
    // спокійно пропускає, і додані в майбутньому нічого не зламають.

    private sealed record MeResponse(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("result")] Me? Result);

    private sealed record Me([property: JsonPropertyName("username")] string? Username);

    private sealed record UpdatesResponse(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("result")] IReadOnlyList<Update>? Result);

    private sealed record Update(
        [property: JsonPropertyName("update_id")] long UpdateId,
        [property: JsonPropertyName("message")] Message? Message);

    private sealed record Message(
        [property: JsonPropertyName("chat")] Chat? Chat,
        [property: JsonPropertyName("text")] string? Text);

    private sealed record Chat(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("first_name")] string? FirstName,
        [property: JsonPropertyName("username")] string? Username);
}
