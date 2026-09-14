import { apiGet } from './client'
import type { ListingSummary } from './catalog'

/**
 * Скільки пунктів історії взагалі зберігається. Те саме число стоїть на
 * сервері: більше він не віддасть, навіть якщо попросити.
 */
export const historyLimit = 100

export function fetchRecentlyViewed(
  take: number,
  signal?: AbortSignal,
): Promise<ListingSummary[]> {
  return apiGet<ListingSummary[]>(`/api/listings/recently-viewed?take=${take}`, signal)
}
