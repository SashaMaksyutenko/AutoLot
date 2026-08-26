using Microsoft.AspNetCore.SignalR;

namespace AutoLot.Api.Realtime;

/// <summary>
/// Живий канал торгів. SignalR тримає з браузером постійне з'єднання, тож
/// сервер може сам надіслати новину, не чекаючи запиту. Без цього довелося б
/// щосекунди опитувати сервер — і на сотні глядачів це тисячі зайвих запитів.
///
/// Автентифікації тут свідомо немає. Через хаб іде лише те, що й так видно
/// всім на сторінці лота: ціна, лічильник ставок, історія. Самі ставки
/// приймаються звичайним HTTP-запитом, де токен перевіряється як завжди —
/// тож відкритий канал нічого не відкриває зайвого.
/// </summary>
public sealed class AuctionHub : Hub
{
    /// <summary>
    /// Клієнт каже, за яким лотом стежить. Група — це список з'єднань, яким
    /// піде та сама розсилка: людина, що дивиться BMW, не отримує новин про
    /// Renault, хоч канал і спільний.
    /// </summary>
    public Task Watch(long listingId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(listingId));
    }

    public Task Unwatch(long listingId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(listingId));
    }

    /// <summary>
    /// Назва групи для лота. Зібрана в одному місці, щоб хаб і розсилка
    /// не розійшлися в написанні: помилка в літері не зламала б збірку,
    /// але новини мовчки перестали б доходити.
    /// </summary>
    public static string GroupFor(long listingId) => $"auction-{listingId}";
}
