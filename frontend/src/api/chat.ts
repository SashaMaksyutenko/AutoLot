import { apiGet, apiPost } from './client'

/**
 * Приватне листування покупця з продавцем.
 *
 * Не плутати з публічними питаннями під лотом: там відповідь бачить кожен,
 * бо стосується авто; тут — час огляду, торг, адреса.
 */

export interface ConversationSummary {
  id: number
  listingId: number
  listingTitle: string
  listingPhotoPath: string | null
  /** Для покупця це продавець, для продавця — покупець. Рахує сервер. */
  companionName: string
  lastMessageText: string | null
  lastMessageAt: string
  unreadCount: number
}

export interface MessageRecord {
  id: number
  conversationId: number
  senderId: number
  senderName: string
  text: string
  createdAt: string
  isRead: boolean
}

export interface ConversationDetails {
  id: number
  listingId: number
  listingTitle: string
  listingPhotoPath: string | null
  companionName: string
  viewerIsSeller: boolean
  messages: MessageRecord[]
}

export function fetchConversations(signal?: AbortSignal): Promise<ConversationSummary[]> {
  return apiGet<ConversationSummary[]>('/api/chat/conversations', signal)
}

export function fetchUnreadCount(signal?: AbortSignal): Promise<{ count: number }> {
  return apiGet<{ count: number }>('/api/chat/unread', signal)
}

/** Відкриває розмову про оголошення або починає нову — одна гілка на лот. */
export function startConversation(listingId: number): Promise<ConversationDetails> {
  return apiPost<ConversationDetails>(`/api/chat/conversations/${listingId}`)
}

export function fetchConversation(
  conversationId: number,
  signal?: AbortSignal,
): Promise<ConversationDetails> {
  return apiGet<ConversationDetails>(
    `/api/chat/conversations/${conversationId}/messages`,
    signal,
  )
}

export function sendMessage(conversationId: number, text: string): Promise<MessageRecord> {
  return apiPost<MessageRecord>(`/api/chat/conversations/${conversationId}/messages`, { text })
}
