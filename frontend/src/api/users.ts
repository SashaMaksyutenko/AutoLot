import { apiGet } from './client'
import type { DealerBadge } from './dealership'
import type { RatingSummary } from './reviews'

/**
 * Профіль продавця очима стороннього.
 *
 * Це не той профіль, що в кабінеті. Тут немає ні пошти, ні телефону, ні
 * ролей — і не тому, що клієнт їх ховає, а тому, що сервер їх не віддає.
 * Сховане на клієнті знаходять за секунду, переглянувши відповідь.
 */
export interface PublicProfile {
  id: number
  displayName: string
  accountType: 'Private' | 'Dealer'

  /** Відколи на майданчику. Найдешевша ознака довіри, яка тут є. */
  joinedAt: string

  cityName: string | null
  rating: RatingSummary
  activeListingCount: number

  /** Салон, якщо людина в ньому працює. */
  dealer: DealerBadge | null
}

export function fetchPublicProfile(
  userId: number,
  signal?: AbortSignal,
): Promise<PublicProfile> {
  return apiGet<PublicProfile>(`/api/users/${userId}`, signal)
}
