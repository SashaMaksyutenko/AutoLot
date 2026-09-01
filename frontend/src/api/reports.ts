import { apiGet, apiPost } from './client'
import type { LookupItem } from './reference'

/**
 * Скарги на оголошення.
 *
 * Це не питання під лотом і не приватний лист. Питання бачать усі, лист —
 * двоє, а скаргу лише модератор: вона адресована майданчику, а не продавцю.
 * Тому тут немає жодної функції «прочитати скарги на оголошення» — автор
 * побачить лише наслідок, якщо його оголошення знімуть.
 */

/** За що скаржаться. Значення надсилаємо назад, назву показуємо людині. */
export type ReportReason =
  | 'Fraud'
  | 'WrongInformation'
  | 'AlreadySold'
  | 'Duplicate'
  | 'Offensive'
  | 'Other'

/** Відповідь на подану скаргу. */
export interface ReportReceipt {
  id: number
  listingId: number
  reason: ReportReason
  createdAt: string
  /** false, якщо така скарга від цієї людини вже була. */
  isNew: boolean
}

/** Скарга в черзі модератора — з усім, що потрібно для рішення. */
export interface ReportSummary {
  id: number
  listingId: number
  listingTitle: string
  listingPhoto: string | null
  listingPrice: number
  reason: ReportReason
  reasonName: string
  comment: string | null
  reporterName: string
  createdAt: string
  /** Скільки ще скарг на це саме оголошення чекають розгляду. */
  otherPendingForListing: number
}

export function fetchReportReasons(signal?: AbortSignal): Promise<LookupItem[]> {
  return apiGet<LookupItem[]>('/api/reports/reasons', signal)
}

export function submitReport(
  listingId: number,
  reason: ReportReason,
  comment: string,
): Promise<ReportReceipt> {
  return apiPost<ReportReceipt>(`/api/reports/listings/${listingId}`, { reason, comment })
}

// ── Модератор ────────────────────────────────────────────────────────

export function fetchReportQueue(signal?: AbortSignal): Promise<ReportSummary[]> {
  return apiGet<ReportSummary[]>('/api/moderation/reports', signal)
}

export function resolveReport(
  reportId: number,
  accepted: boolean,
  note: string,
): Promise<void> {
  return apiPost<void>(`/api/moderation/reports/${reportId}/resolve`, { accepted, note })
}
