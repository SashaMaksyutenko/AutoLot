import { apiGet, apiPost, apiPut } from './client'

/**
 * Публічні питання під оголошенням. Читати може будь-хто, зокрема гість —
 * у цьому й сенс: відповідь одному покупцеві цікавить усіх, хто дивиться лот.
 */

export interface QuestionRecord {
  id: number
  askerName: string
  text: string
  createdAt: string
  /** Порожня, поки продавець не відповів. */
  answer: string | null
  answeredAt: string | null
}

export function fetchQuestions(
  listingId: number,
  signal?: AbortSignal,
): Promise<QuestionRecord[]> {
  return apiGet<QuestionRecord[]>(`/api/listings/${listingId}/questions`, signal)
}

export function askQuestion(listingId: number, text: string): Promise<QuestionRecord> {
  return apiPost<QuestionRecord>(`/api/listings/${listingId}/questions`, { text })
}

export function answerQuestion(
  listingId: number,
  questionId: number,
  text: string,
): Promise<QuestionRecord> {
  return apiPut<QuestionRecord>(
    `/api/listings/${listingId}/questions/${questionId}/answer`,
    { text },
  )
}
