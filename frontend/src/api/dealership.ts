import { apiDelete, apiGet, apiPost, apiPut } from './client'

/**
 * Автосалони. Картка й каталог відкриті всім — це вітрина; усе, що
 * стосується персоналу, лишається на бекенді за перевіркою прав.
 */

export interface DealerBadge {
  name: string
  slug: string
  isVerified: boolean
}

export interface DealershipCard {
  id: number
  name: string
  slug: string
  logoPath: string | null
  cityName: string
  isVerified: boolean
  activeListingCount: number
}

export interface DealershipDetails extends DealershipCard {
  description: string | null
  verifiedAt: string | null
}

export function fetchDealerships(
  params: { text?: string; cityId?: number; verifiedOnly?: boolean },
  signal?: AbortSignal,
): Promise<DealershipCard[]> {
  const query = new URLSearchParams()

  if (params.text) query.set('text', params.text)
  if (params.cityId !== undefined) query.set('cityId', String(params.cityId))
  if (params.verifiedOnly) query.set('verifiedOnly', 'true')

  return apiGet<DealershipCard[]>(`/api/dealerships?${query}`, signal)
}

export function fetchDealership(slug: string, signal?: AbortSignal): Promise<DealershipDetails> {
  return apiGet<DealershipDetails>(`/api/dealerships/${slug}`, signal)
}

/** Салон, у якому працює користувач, і його роль там. */
export interface DealershipMembership {
  dealershipId: number
  name: string
  slug: string
  role: 'Owner' | 'Manager'
  isVerified: boolean
}

export function fetchMyDealerships(signal?: AbortSignal): Promise<DealershipMembership[]> {
  return apiGet<DealershipMembership[]>('/api/dealerships/mine', signal)
}

/** Роль у салоні. Власник керує персоналом, менеджер лише працює з авто. */
export type DealershipRole = 'Owner' | 'Manager'

export interface StaffMember {
  userId: number
  displayName: string
  email: string
  role: DealershipRole
  joinedAt: string
}

export interface CreateDealershipRequest {
  name: string
  description?: string | null
  cityId: number
}

export function createDealership(
  request: CreateDealershipRequest,
): Promise<DealershipDetails> {
  return apiPost<DealershipDetails>('/api/dealerships', request)
}

/**
 * Персонал салону. Перелік бачить будь-який працівник — і власник, і
 * менеджер: знати, з ким працюєш, потрібно обом. Змінювати його може лише
 * власник, і це вирішує сервер, а не приховані кнопки.
 */
export function fetchStaff(dealershipId: number, signal?: AbortSignal): Promise<StaffMember[]> {
  return apiGet<StaffMember[]>(`/api/dealerships/${dealershipId}/staff`, signal)
}

/**
 * Додають за поштою, а не за ідентифікатором: власник знає адресу колеги,
 * а не його номер у базі. Людина вже має бути зареєстрованою — сервер
 * відповість зрозумілим текстом, якщо ні.
 */
export function addStaff(
  dealershipId: number,
  email: string,
  role: DealershipRole,
): Promise<void> {
  return apiPost<void>(`/api/dealerships/${dealershipId}/staff`, { email, role })
}

export function removeStaff(dealershipId: number, userId: number): Promise<void> {
  return apiDelete<void>(`/api/dealerships/${dealershipId}/staff/${userId}`)
}

/** Бейдж перевіреного ставить модератор — це дія майданчика, а не салону. */
export function setVerification(dealershipId: number, verified: boolean): Promise<void> {
  return apiPut<void>(`/api/dealerships/${dealershipId}/verification?verified=${verified}`)
}
