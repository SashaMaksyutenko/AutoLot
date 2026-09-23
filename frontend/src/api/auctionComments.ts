import { apiGet, apiPost } from './client'

/** Коментар під лотом так, як його бачать усі. */
export interface AuctionComment {
  readonly id: number
  readonly listingId: number
  readonly authorName: string

  /** Написав продавець або хтось із його салону — позначаємо окремо. */
  readonly isSeller: boolean
  readonly text: string
  readonly createdAt: string
}

/** Та сама межа, що на сервері (AuctionComment.MaxLength). */
export const commentMaxLength = 1000

export function fetchComments(listingId: number, signal?: AbortSignal): Promise<AuctionComment[]> {
  return apiGet<AuctionComment[]>(`/api/listings/${listingId}/auction/comments`, signal)
}

export function postComment(listingId: number, text: string): Promise<AuctionComment> {
  return apiPost<AuctionComment>(`/api/listings/${listingId}/auction/comments`, { text })
}
