import { apiDelete, apiGet, apiPost } from './client'
import type { ListingSummary } from './catalog'

/**
 * Власні оголошення та дії з ними.
 *
 * Це не каталог: там пошук серед чужих, тут керування своїми. Тому й
 * оголошення приходять з усіма статусами — чернетки й відхилені теж, бо
 * саме вони й потребують уваги господаря.
 */

/** Той, кому продавець міг продати авто. */
export interface BuyerCandidate {
  id: number
  displayName: string
  lastMessageAt: string
  /** Переможець торгів. Для аукціонного лота він у списку єдиний. */
  isAuctionWinner: boolean
}

export function fetchMyListings(
  status?: string,
  signal?: AbortSignal,
): Promise<ListingSummary[]> {
  const query = status ? `?status=${status}` : ''

  return apiGet<ListingSummary[]>(`/api/listings/mine${query}`, signal)
}

export function fetchBuyerCandidates(
  listingId: number,
  signal?: AbortSignal,
): Promise<BuyerCandidate[]> {
  return apiGet<BuyerCandidate[]>(`/api/listings/${listingId}/buyer-candidates`, signal)
}

/** Порожній buyerId означає «продано поза майданчиком». */
export function markSold(listingId: number, buyerId: number | null): Promise<void> {
  return apiPost<void>(`/api/listings/${listingId}/sold`, { buyerId })
}

export function submitForModeration(listingId: number): Promise<void> {
  return apiPost<void>(`/api/listings/${listingId}/submit`)
}

export function archiveListing(listingId: number): Promise<void> {
  return apiPost<void>(`/api/listings/${listingId}/archive`)
}

/** Видалити можна лише чернетку — решта архівується. */
export function deleteDraft(listingId: number): Promise<void> {
  return apiDelete<void>(`/api/listings/${listingId}`)
}
