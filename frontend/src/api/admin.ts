import { apiGet, apiPost, apiPut } from './client'
import type { AccountType } from './auth'
import type { ListingSummary, PagedResult } from './catalog'

/**
 * Адмінка й модерація.
 *
 * Це два різні набори прав, і плутати їх не можна: модератор працює з
 * ОГОЛОШЕННЯМИ (черга, схвалення, відмови), адміністратор — з ЛЮДЬМИ
 * (блокування, ролі). Тому й адреси різні, і роль на кожній свою.
 */

export interface PlatformStats {
  totalUsers: number
  bannedUsers: number
  activeListings: number
  pendingModeration: number
  /** Скарги, що чекають розгляду. Черга окрема від модерації. */
  pendingReports: number
  activeAuctions: number
  dealerships: number
  unverifiedDealerships: number
}

export interface UserSummary {
  id: number
  displayName: string
  email: string
  accountType: AccountType
  isBanned: boolean
  emailConfirmed: boolean
  createdAt: string
  lastLoginAt: string | null
  roles: string[]
  activeListingCount: number
}

// ── Адміністратор ────────────────────────────────────────────────────

export function fetchStats(signal?: AbortSignal): Promise<PlatformStats> {
  return apiGet<PlatformStats>('/api/admin/stats', signal)
}

export function fetchUsers(
  params: { text?: string; isBanned?: boolean; role?: string; page: number },
  signal?: AbortSignal,
): Promise<PagedResult<UserSummary>> {
  const query = new URLSearchParams({ page: String(params.page) })

  if (params.text) query.set('text', params.text)
  if (params.isBanned !== undefined) query.set('isBanned', String(params.isBanned))
  if (params.role) query.set('role', params.role)

  return apiGet<PagedResult<UserSummary>>(`/api/admin/users?${query}`, signal)
}

export function setUserBanned(userId: number, isBanned: boolean): Promise<void> {
  return apiPut<void>(`/api/admin/users/${userId}/ban`, { isBanned })
}

export function setUserRole(userId: number, role: string, granted: boolean): Promise<void> {
  return apiPut<void>(`/api/admin/users/${userId}/roles`, { role, granted })
}

// ── Модератор ────────────────────────────────────────────────────────

export function fetchModerationQueue(signal?: AbortSignal): Promise<ListingSummary[]> {
  return apiGet<ListingSummary[]>('/api/moderation/listings', signal)
}

export function approveListing(listingId: number): Promise<void> {
  return apiPost<void>(`/api/moderation/listings/${listingId}/approve`)
}

export function rejectListing(listingId: number, reason: string): Promise<void> {
  return apiPost<void>(`/api/moderation/listings/${listingId}/reject`, { reason })
}
