using AutoLot.Application.Chat.Dtos;

namespace AutoLot.Application.Chat;

/// <summary>
/// Доставляє повідомлення тим, хто зараз на сайті.
///
/// Оголошено тут, а реалізовано в шарі Api — з тієї ж причини, що й розсилка
/// торгів: SignalR це подробиця вебсервера, і Infrastructure не має про неї
/// знати.
/// </summary>
public interface IChatNotifier
{
    Task MessageSentAsync(
        IReadOnlyList<long> recipientIds,
        MessageRecord message,
        CancellationToken cancellationToken = default);
}
