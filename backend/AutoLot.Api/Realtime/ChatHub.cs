using AutoLot.Application.Chat;
using AutoLot.Application.Chat.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AutoLot.Api.Realtime;

/// <summary>
/// Живий канал листування.
///
/// На відміну від аукціонного хаба цей **закритий**: через торги йде те, що
/// й так видно всім на сторінці лота, а тут — приватна переписка. Без
/// автентифікації будь-хто підписався б на чужі повідомлення.
///
/// Груп заводити не треба: SignalR сам тримає групу на кожного користувача
/// за його ідентифікатором із токена, і надіслати «цій людині, де б вона не
/// сиділа» можна одним викликом.
/// </summary>
[Authorize]
public sealed class ChatHub : Hub;

/// <summary>
/// Реалізація доставки. Живе в шарі Api, бо SignalR — подробиця вебсервера:
/// сценаріям достатньо інтерфейсу <see cref="IChatNotifier"/>.
/// </summary>
internal sealed class SignalRChatNotifier(IHubContext<ChatHub> hub) : IChatNotifier
{
    public Task MessageSentAsync(
        IReadOnlyList<long> recipientIds,
        MessageRecord message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipientIds);

        // Users приймає рядки — саме в такому вигляді ідентифікатор лежить
        // у токені, і SignalR звіряє його як текст.
        var ids = recipientIds
            .Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();

        // "messageSent" — назва, на яку підписується браузер. Має збігатися
        // з тією, що у фронтенді (src/api/chatHub.ts).
        return hub.Clients.Users(ids).SendAsync("messageSent", message, cancellationToken);
    }
}
