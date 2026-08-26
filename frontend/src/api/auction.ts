import { apiGet, apiPost } from './client'
import type { Currency } from './catalog'

export type AuctionStatus = 'Active' | 'Ended' | 'Cancelled'

/** Стан торгів. Суми резерву й чужої стелі тут немає й ніколи не буде. */
export interface AuctionDetails {
  listingId: number
  currency: Currency
  startPrice: number
  currentPrice: number
  minimumNextBid: number
  bidStep: number
  bidCount: number
  startsAt: string
  endsAt: string
  status: AuctionStatus
  hasReserve: boolean
  isReserveMet: boolean
  leaderName: string | null
  isViewerLeading: boolean
  canViewerBid: boolean
  leaderId: number | null

  /** Час сервера в мить відповіді — за ним вивіряється таймер. */
  serverTime: string
}

export interface BidRecord {
  id: number
  bidderName: string
  amount: number
  isAutomatic: boolean
  createdAt: string
}

/**
 * Новина з живого каналу. Нічого особистого: розсилка одна на всіх глядачів,
 * тож «чи лідирую я» кожен добудовує сам, звіряючи leaderId зі своїм.
 */
export interface AuctionUpdate {
  listingId: number
  currentPrice: number
  minimumNextBid: number
  bidStep: number
  bidCount: number
  endsAt: string
  status: AuctionStatus
  isReserveMet: boolean
  leaderId: number | null
  leaderName: string | null
  newBids: BidRecord[]
  serverTime: string
}

/**
 * Підсумок торгів. Переможця може й не бути: лот міг не зібрати жодної
 * ставки або не дотягнути до резерву — тоді причину пояснює isReserveMet.
 */
export interface AuctionOutcome {
  listingId: number
  finalPrice: number
  currency: Currency
  bidCount: number
  winnerId: number | null
  winnerName: string | null
  isReserveMet: boolean
  endedAt: string
  serverTime: string
}

export function fetchAuction(listingId: number, signal?: AbortSignal): Promise<AuctionDetails> {
  return apiGet<AuctionDetails>(`/api/listings/${listingId}/auction`, signal)
}

export function fetchBidHistory(listingId: number, signal?: AbortSignal): Promise<BidRecord[]> {
  return apiGet<BidRecord[]>(`/api/listings/${listingId}/auction/bids`, signal)
}

/**
 * maxAmount — це СТЕЛЯ, а не сума платежу. Система поставить рівно стільки,
 * скільки треба для лідерства, і сама підніматиме, поки стелі вистачає.
 */
export function placeBid(listingId: number, maxAmount: number): Promise<AuctionDetails> {
  return apiPost<AuctionDetails>(`/api/listings/${listingId}/auction/bids`, { maxAmount })
}
