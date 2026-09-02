import { apiGet, apiPost } from './client'

/**
 * Взаємні відгуки після угоди.
 *
 * Читання відкрите всім, зокрема гостю: у публічності вся користь відгуку —
 * покупець має бачити репутацію продавця ДО того, як напише йому.
 *
 * Зверніть увагу, чого тут немає: поля «про кого». Сервер виводить це сам
 * зі складу угоди, тож приписати відгук сторонньому неможливо навіть
 * підробленим запитом.
 */

export interface ReviewRecord {
  id: number
  listingId: number
  listingTitle: string
  authorId: number
  authorName: string
  subjectId: number
  rating: number
  text: string | null
  createdAt: string
  /** Хто написав: за цим підпис «відгук продавця» чи «відгук покупця». */
  authorIsSeller: boolean
}

/** Нуль відгуків — це не нуль зірок, тому count тут обов'язковий. */
export interface RatingSummary {
  count: number
  average: number
}

/** Стан відгуків під однією угодою очима того, хто дивиться. */
export interface DealReviews {
  canReview: boolean

  /**
   * Усі відгуки про цю угоду, власний теж. Саме список, а не пара
   * «мій / чужий»: у гостя «свого» немає, а бачити чужі він має — заради
   * нього відгуки й публічні.
   */
  reviews: ReviewRecord[]

  /** Ідентифікатор власного відгуку, якщо він є. */
  mineId: number | null
}

export function fetchDealReviews(
  listingId: number,
  signal?: AbortSignal,
): Promise<DealReviews> {
  return apiGet<DealReviews>(`/api/listings/${listingId}/reviews`, signal)
}

export function leaveReview(
  listingId: number,
  rating: number,
  text: string,
): Promise<ReviewRecord> {
  return apiPost<ReviewRecord>(`/api/listings/${listingId}/reviews`, { rating, text })
}

export function fetchReviewsAbout(
  userId: number,
  signal?: AbortSignal,
): Promise<ReviewRecord[]> {
  return apiGet<ReviewRecord[]>(`/api/users/${userId}/reviews`, signal)
}
