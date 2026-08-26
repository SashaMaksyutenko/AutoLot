import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import type { HubConnection } from '@microsoft/signalr'
import type { AuctionUpdate } from './auction'

/**
 * Живий канал торгів.
 *
 * Звичайний запит працює так: браузер питає — сервер відповідає. Тут навпаки:
 * з'єднання лишається відкритим, і сервер сам надсилає новину, щойно хтось
 * зробив ставку. Без цього довелося б щосекунди перепитувати сервер, і на
 * сотні глядачів це тисячі зайвих запитів.
 *
 * З'єднання одне на весь застосунок: відкривати окреме на кожну сторінку —
 * марно витрачати ресурс, а браузери ще й обмежують кількість таких каналів.
 */

let connection: HubConnection | null = null

function getConnection(): HubConnection {
  connection ??= new HubConnectionBuilder()
    .withUrl('/hubs/auction')

    // Мережа в мобільних падає постійно. withAutomaticReconnect сам
    // відновлює з'єднання з дедалі більшими паузами, замість того щоб
    // мовчки лишити сторінку з застиглою ціною.
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()

  return connection
}

/**
 * Підписує на новини одного лота й повертає функцію відписки.
 *
 * Повертати саме відписку — домовленість React: він викличе її, коли
 * сторінка зникне з екрана, і канал не лишиться з мертвими підписниками.
 */
export function watchAuction(
  listingId: number,
  onUpdate: (update: AuctionUpdate) => void,
): () => void {
  const hub = getConnection()

  // Прапорець живе тут, бо підключення асинхронне: сторінку можуть закрити
  // швидше, ніж канал устигне відкритися, і тоді підписуватися вже нікуди.
  let cancelled = false

  function handle(update: AuctionUpdate) {
    // Група на сервері вже відсіює чуже, але перевірка дешева, а помилка
    // в назві групи інакше проявилася б як чужі ставки на своєму лоті.
    if (update.listingId === listingId) {
      onUpdate(update)
    }
  }

  hub.on('bidPlaced', handle)

  const ready =
    hub.state === HubConnectionState.Disconnected ? hub.start() : Promise.resolve()

  void ready
    .then(() => {
      if (!cancelled) {
        return hub.invoke('Watch', listingId)
      }
    })
    .catch(() => {
      // Канал не піднявся. Це не привід ламати сторінку: ціна й історія вже
      // прийшли звичайним запитом, просто оновлюватися самі не будуть.
    })

  return () => {
    cancelled = true
    hub.off('bidPlaced', handle)

    if (hub.state === HubConnectionState.Connected) {
      void hub.invoke('Unwatch', listingId).catch(() => {})
    }
  }
}
