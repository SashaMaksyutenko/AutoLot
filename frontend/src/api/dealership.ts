import { apiGet } from './client'

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
