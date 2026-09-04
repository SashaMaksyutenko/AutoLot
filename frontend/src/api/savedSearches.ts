import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { CatalogFilters } from './catalog'

/**
 * Збережені пошуки.
 *
 * На сервері фільтри лежать одним рядком JSON, але назовні віддаються
 * об'єктом — таким самим за формою, як фільтри каталогу. Тому відновити
 * сторінку з пошуку означає просто підставити цей об'єкт у стан фільтрів,
 * без жодного розбору на клієнті.
 */

export interface SavedSearchCard {
  id: number
  name: string

  /**
   * Самі фільтри. Тип той самий, що й у каталозі: сервер серіалізує
   * перелічення рядками, а імена властивостей — з малої літери, тож
   * об'єкт лягає в стан як є.
   */
  query: CatalogFilters

  /** Скільки авто підходить прямо зараз. */
  matchCount: number

  /** Чи надсилати листи про нові збіги. */
  notifyByEmail: boolean
  createdAt: string
}

export function fetchSavedSearches(signal?: AbortSignal): Promise<SavedSearchCard[]> {
  return apiGet<SavedSearchCard[]>('/api/saved-searches', signal)
}

export function saveSearch(name: string, query: CatalogFilters): Promise<SavedSearchCard> {
  return apiPost<SavedSearchCard>('/api/saved-searches', { name, query })
}

export function renameSearch(searchId: number, name: string): Promise<SavedSearchCard> {
  return apiPut<SavedSearchCard>(`/api/saved-searches/${searchId}`, { name })
}

/**
 * Вмикає або вимикає листи. Увімкнення рахує «новим» лише те, що з'явиться
 * далі — інакше перший лист приніс би весь каталог, що підходить під фільтр.
 */
export function setSearchNotifications(
  searchId: number,
  enabled: boolean,
): Promise<SavedSearchCard> {
  return apiPut<SavedSearchCard>(`/api/saved-searches/${searchId}/notifications`, { enabled })
}

export function deleteSearch(searchId: number): Promise<void> {
  return apiDelete<void>(`/api/saved-searches/${searchId}`)
}
