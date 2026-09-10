import { apiDelete, apiGet, apiPost, apiPut, apiUpload } from './client'
import type { ListingPhoto } from './listing'

/**
 * Фото оголошення — з боку автора.
 *
 * Читання теж захищене: цей перелік показує фото ще неопублікованого
 * оголошення, тож сервер віддає його лише власникові.
 */

/** Скільки фото приймає одне оголошення. Те саме число стоїть на сервері. */
export const maxPhotos = 20

/** Скільки важить один файл. Більше сервер відхилить, не читаючи. */
export const maxPhotoBytes = 12 * 1024 * 1024

export function fetchPhotos(listingId: number, signal?: AbortSignal): Promise<ListingPhoto[]> {
  return apiGet<ListingPhoto[]>(`/api/listings/${listingId}/photos`, signal)
}

/**
 * Одне фото за раз — так само, як приймає сервер. Кілька обраних файлів
 * форма надсилає послідовно: інакше «додати п'ятнадцять» перетворилося б на
 * п'ятнадцять паралельних запитів, і порядок фото став би випадковим.
 */
export function uploadPhoto(listingId: number, file: File): Promise<ListingPhoto> {
  return apiUpload<ListingPhoto>(`/api/listings/${listingId}/photos`, file)
}

export function deletePhoto(listingId: number, photoId: number): Promise<void> {
  return apiDelete<void>(`/api/listings/${listingId}/photos/${photoId}`)
}

/** Порядок задається повним переліком у потрібній послідовності. */
export function reorderPhotos(listingId: number, photoIds: number[]): Promise<void> {
  return apiPut<void>(`/api/listings/${listingId}/photos/order`, photoIds)
}

export function setPrimaryPhoto(listingId: number, photoId: number): Promise<void> {
  return apiPost<void>(`/api/listings/${listingId}/photos/${photoId}/primary`)
}
