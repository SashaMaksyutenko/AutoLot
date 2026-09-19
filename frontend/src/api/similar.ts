import { apiGet } from './client'
import type { ListingSummary } from './catalog'

/**
 * Схожі авто: тієї самої марки, у межах чверті ціни, спершу тієї самої
 * моделі. Сервер уже впорядковує їх як треба — тут лишається тільки показати.
 */
export function fetchSimilar(
  listingId: number,
  take: number,
  signal?: AbortSignal,
): Promise<ListingSummary[]> {
  return apiGet<ListingSummary[]>(`/api/listings/${listingId}/similar?take=${take}`, signal)
}
