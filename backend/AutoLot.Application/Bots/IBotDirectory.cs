namespace AutoLot.Application.Bots;

/// <summary>Що кабінет має знати про бота, щоб дати на нього посилання.</summary>
public sealed record TelegramBotInfo(bool Enabled, string? Username);

/// <summary>
/// Відомості про самих ботів — на відміну від <see cref="IBotLinkService"/>,
/// який знає про людей.
/// </summary>
public interface IBotDirectory
{
    Task<TelegramBotInfo> GetTelegramAsync(CancellationToken cancellationToken = default);
}
