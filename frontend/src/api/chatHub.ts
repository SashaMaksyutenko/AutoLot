import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import type { HubConnection } from '@microsoft/signalr'
import type { MessageRecord } from './chat'
import { getAccessToken } from '../auth/tokenStore'

/**
 * Живий канал листування.
 *
 * На відміну від каналу торгів цей **закритий**: через нього йде приватна
 * переписка, тож сервер вимагає токен.
 *
 * Токен передається в адресі, а не заголовком — і це не недбалість.
 * Браузерний WebSocket просто не має способу надіслати заголовок
 * Authorization; так роблять усі, і сервер приймає токен з адреси лише для
 * шляхів /hubs.
 *
 * accessTokenFactory викликається на КОЖНЕ підключення, зокрема при
 * автоматичному відновленні. Тому читаємо токен щоразу заново: він живе
 * п'ятнадцять хвилин, і збережений при першому підключенні давно б протух.
 */

let connection: HubConnection | null = null

function getConnection(): HubConnection {
  connection ??= new HubConnectionBuilder()
    .withUrl('/hubs/chat', {
      accessTokenFactory: () => getAccessToken() ?? '',
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()

  return connection
}

/**
 * Підписує на нові повідомлення й повертає функцію відписки.
 *
 * Групи тут не потрібні: сервер надсилає адресно тому, кому повідомлення
 * призначене, — за ідентифікатором із токена.
 */
export function watchChat(onMessage: (message: MessageRecord) => void): () => void {
  const hub = getConnection()

  let cancelled = false

  hub.on('messageSent', onMessage)

  if (hub.state === HubConnectionState.Disconnected) {
    hub
      .start()
      .then(() => {
        if (cancelled) {
          void hub.stop()
        }
      })
      .catch(() => {
        // Канал не піднявся — не привід ламати сторінку. Листування вже
        // прийшло звичайним запитом, просто не оновлюватиметься саме.
      })
  }

  return () => {
    cancelled = true
    hub.off('messageSent', onMessage)
  }
}

/**
 * Розриває канал. Потрібне при виході з акаунта: інакше з'єднання лишилося б
 * відкритим зі старим токеном, і наступний користувач у тому самому браузері
 * отримував би чужі повідомлення.
 */
export async function closeChat(): Promise<void> {
  if (connection === null) {
    return
  }

  const hub = connection
  connection = null

  await hub.stop().catch(() => {})
}
