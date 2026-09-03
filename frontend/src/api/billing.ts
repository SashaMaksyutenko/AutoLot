import { apiGet, apiPost } from './client'

/**
 * Гаманець і тарифи.
 *
 * Справжніх платежів у проєкті немає — усе живе на віртуальних одиницях
 * (SPEC, розділ «Не входить»). Поповнення нараховує суму одразу; це показ
 * механіки, а не заглушка на місці платіжної системи.
 */

export type WalletOperation = 'TopUp' | 'SubscriptionCharge' | 'Refund'

/** Один рядок історії руху коштів. */
export interface WalletEntry {
  id: number
  /** Зі знаком: додатна — надходження, від'ємна — списання. */
  amount: number
  kind: WalletOperation
  balanceAfter: number
  createdAt: string
}

export interface WalletState {
  balance: number
  recent: WalletEntry[]
}

export interface PlanCard {
  id: number
  code: string
  name: string
  description: string
  price: number
  durationDays: number
  /** null — без обмеження на кількість оголошень. */
  listingLimit: number | null
  isDefault: boolean
  isCurrent: boolean
}

export interface SubscriptionState {
  plan: PlanCard
  /** Доки оплачено. Порожнє в безкоштовного плану — він безстроковий. */
  activeUntil: string | null
  activeListings: number
}

export function fetchWallet(signal?: AbortSignal): Promise<WalletState> {
  return apiGet<WalletState>('/api/billing/wallet', signal)
}

export function topUpWallet(amount: number): Promise<WalletState> {
  return apiPost<WalletState>('/api/billing/wallet/top-up', { amount })
}

export function fetchPlans(signal?: AbortSignal): Promise<PlanCard[]> {
  return apiGet<PlanCard[]>('/api/billing/plans', signal)
}

export function fetchSubscription(signal?: AbortSignal): Promise<SubscriptionState> {
  return apiGet<SubscriptionState>('/api/billing/subscription', signal)
}

export function subscribe(planCode: string): Promise<SubscriptionState> {
  return apiPost<SubscriptionState>(`/api/billing/subscription/${planCode}`)
}
