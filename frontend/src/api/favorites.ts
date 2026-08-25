import { apiDelete, apiGet, apiPut } from './client'
import type { ListingSummary, PagedResult } from './catalog'

/**
 * Обране. Ідентифікатора користувача в адресах немає взагалі — бекенд бере
 * його з токена, тож зазирнути в чужий список неможливо.
 */

export function fetchFavorites(
  page: number,
  signal?: AbortSignal,
): Promise<PagedResult<ListingSummary>> {
  return apiGet<PagedResult<ListingSummary>>(`/api/favorites?page=${page}`, signal)
}

export function fetchFavoriteCount(signal?: AbortSignal): Promise<{ count: number }> {
  return apiGet<{ count: number }>('/api/favorites/count', signal)
}

export function addFavorite(listingId: number): Promise<void> {
  return apiPut<void>(`/api/favorites/${listingId}`)
}

export function removeFavorite(listingId: number): Promise<void> {
  return apiDelete<void>(`/api/favorites/${listingId}`)
}
