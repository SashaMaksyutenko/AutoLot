import { apiGet, apiPost } from './client'

/**
 * Прив'язка месенджерів до акаунта.
 *
 * Бот не бачить, хто ви: у чаті він знає лише розмову. Тому людина бере в
 * кабінеті одноразовий код і надсилає його боту — так чат і акаунт
 * зустрічаються, жодного разу не назвавши пароль.
 */

export type BotProvider = 'Telegram' | 'Viber'

export interface TelegramBotInfo {
  /** Чи піднятий бот узагалі: без токена його просто немає. */
  enabled: boolean

  /** Ім'я бота в Telegram — з нього будується посилання. */
  username: string | null
}

export interface BotLinkCode {
  code: string
  expiresAt: string
}

export function fetchTelegramBot(signal?: AbortSignal): Promise<TelegramBotInfo> {
  return apiGet<TelegramBotInfo>('/api/bots/telegram', signal)
}

export function fetchBotLinks(signal?: AbortSignal): Promise<BotProvider[]> {
  return apiGet<BotProvider[]>('/api/bots/links', signal)
}

/** Видає новий код. Попередній при цьому перестає діяти. */
export function issueLinkCode(): Promise<BotLinkCode> {
  return apiPost<BotLinkCode>('/api/bots/link-code')
}
