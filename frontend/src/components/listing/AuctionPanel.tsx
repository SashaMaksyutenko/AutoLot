import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  fetchAuction,
  fetchBidHistory,
  placeBid,
  type AuctionDetails,
  type AuctionOutcome,
  type AuctionUpdate,
  type BidRecord,
} from '../../api/auction'
import { watchAuction } from '../../api/auctionHub'
import { ApiError } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { openSignIn } from '../../auth/signInPrompt'
import { formatDateTime, formatPrice } from '../../format'
import { useTranslation } from '../../i18n/useTranslation'
import { formatRemaining, useCountdown } from './useCountdown'

/** Менше за стільки до кінця — торги вважаємо гарячими й підсвічуємо таймер. */
const UrgentMilliseconds = 10 * 60 * 1000

export function AuctionPanel({ listingId }: { listingId: number }) {
  const { t } = useTranslation()

  const auth = useAuth()

  const initial = useQuery({
    queryKey: ['auction', listingId],
    queryFn: ({ signal }) => fetchAuction(listingId, signal),
  })

  const initialHistory = useQuery({
    queryKey: ['auction-bids', listingId],
    queryFn: ({ signal }) => fetchBidHistory(listingId, signal),
  })

  /*
    Стан торгів тримаємо тут, а не в кеші запитів: він приходить із двох
    джерел — звичайного запиту при відкритті сторінки і живого каналу далі.
    Один локальний стан, у який зливаються обидва, простіший за спроби
    правити чужий кеш ззовні.
  */
  const [auction, setAuction] = useState<AuctionDetails | null>(null)
  const [history, setHistory] = useState<BidRecord[]>([])

  useEffect(() => {
    if (initial.data) setAuction(initial.data)
  }, [initial.data])

  useEffect(() => {
    if (initialHistory.data) setHistory(initialHistory.data)
  }, [initialHistory.data])

  const [outcome, setOutcome] = useState<AuctionOutcome | null>(null)

  useEffect(() => {
    return watchAuction(listingId, {
      onUpdate: (update: AuctionUpdate) => {
        setAuction((current) => (current ? merge(current, update, auth.user?.id) : current))

        // Нові рядки приходять найновішим першим — саме так вони й лежать
        // у списку, тож просто додаємо їх на початок.
        setHistory((current) => [...update.newBids, ...current])
      },

      onEnded: (ended: AuctionOutcome) => {
        setOutcome(ended)

        // Статус міняємо й у самому стані: інакше форма ставки лишилася б
        // на екрані, хоч приймати їх уже нікому.
        setAuction((current) =>
          current ? { ...current, status: 'Ended', canViewerBid: false } : current,
        )
      },
    })
  }, [listingId, auth.user?.id])

  if (initial.isPending) {
    return <div className="card p-4 text-sm text-ink-2">{t('auction.loading')}</div>
  }

  if (!auction) {
    return null
  }

  return (
    <>
      <Panel auction={auction} onSignIn={openSignIn} isAuthenticated={auth.user !== null} />
      {outcome && <Outcome outcome={outcome} isViewerWinner={outcome.winnerId === auth.user?.id} />}
      <History bids={history} currency={auction.currency} />
    </>
  )
}

/**
 * Підсумок торгів. З'являється в мить закриття — щоб людина не гадала, чому
 * таймер завмер на нулях, а одразу побачила, чим усе скінчилося.
 */
function Outcome({
  outcome,
  isViewerWinner,
}: {
  outcome: AuctionOutcome
  isViewerWinner: boolean
}) {
  const { t } = useTranslation()

  if (isViewerWinner) {
    return (
      <div className="card border-good p-4">
        <h2 className="eyebrow mb-1">{t('auction.finished')}</h2>
        <p className="text-[15px] font-semibold text-good">
          {t('auction.youWon', {
            price: formatPrice(outcome.finalPrice, outcome.currency),
          })}
        </p>
        <p className="mt-1 text-[13px] text-ink-2">{t('auction.contactSeller')}</p>
      </div>
    )
  }

  if (outcome.winnerId !== null) {
    return (
      <div className="card p-4">
        <h2 className="eyebrow mb-1">{t('auction.finished')}</h2>
        <p className="text-[14px]">
          {t('auction.wonBy', { name: outcome.winnerName ?? '' })}{' '}
          <span className="font-mono font-semibold tabular-nums">
            {formatPrice(outcome.finalPrice, outcome.currency)}
          </span>
        </p>
      </div>
    )
  }

  // Переможця немає — і причини рівно дві.
  return (
    <div className="card p-4">
      <h2 className="eyebrow mb-1">{t('auction.finished')}</h2>
      <p className="text-[14px] text-ink-2">
        {outcome.bidCount === 0
          ? t('auction.noBidsAtAll')
          : t('auction.reserveNotMet')}
      </p>
    </div>
  )
}

/**
 * Зливає живу новину з поточним станом. Особисте («чи лідирую я») у розсилці
 * не приходить — воно в кожного глядача своє, тож обчислюємо тут.
 */
function merge(
  current: AuctionDetails,
  update: AuctionUpdate,
  viewerId: number | undefined,
): AuctionDetails {
  return {
    ...current,
    currentPrice: update.currentPrice,
    minimumNextBid: update.minimumNextBid,
    bidStep: update.bidStep,
    bidCount: update.bidCount,
    endsAt: update.endsAt,
    status: update.status,
    isReserveMet: update.isReserveMet,
    leaderId: update.leaderId,
    leaderName: update.leaderName,
    serverTime: update.serverTime,
    isViewerLeading: viewerId !== undefined && update.leaderId === viewerId,
    canViewerBid: current.canViewerBid && update.status === 'Active',
  }
}

function Panel({
  auction,
  isAuthenticated,
  onSignIn,
}: {
  auction: AuctionDetails
  isAuthenticated: boolean
  onSignIn: () => void
}) {
  const { t, tPlural } = useTranslation()

  const remaining = useCountdown(auction.endsAt, auction.serverTime)
  const isOver = remaining <= 0 || auction.status !== 'Active'

  return (
    <div className="card grid gap-3 border-signal p-4">
      <div className="flex items-center justify-between gap-2">
        <span className="pill pill-live">
          <i className="dot" />
          {t('auction.badge')}
        </span>
        {auction.hasReserve ? (
          <span className={`pill ${auction.isReserveMet ? 'pill-good' : 'pill-danger'}`}>
            {auction.isReserveMet ? t('auction.reserveMet') : t('auction.reserveUnmet')}
          </span>
        ) : (
          <span className="pill pill-good">{t('auction.noReserve')}</span>
        )}
      </div>

      <div>
        <div className="eyebrow">
          {auction.bidCount > 0 ? t('auction.currentBid') : t('auction.startingPrice')}
        </div>
        <div className="font-display text-[30px] leading-tight font-bold text-signal tabular-nums">
          {formatPrice(auction.currentPrice, auction.currency)}
        </div>
        <div className="text-[13px] text-ink-2">
          {tPlural('auction.bids', auction.bidCount)}
          {auction.leaderName
            ? t('auction.leader', { name: auction.leaderName })
            : ''}
        </div>
      </div>

      <div
        className={`rounded-control px-3 py-2 text-center ${
          isOver
            ? 'bg-surface-2 text-ink-2'
            : remaining < UrgentMilliseconds
              ? 'bg-danger-soft text-danger'
              : 'bg-surface-2 text-ink'
        }`}
      >
        <div className="eyebrow">{isOver ? t('auction.over') : t('auction.timeLeft')}</div>
        <div className="font-mono text-[22px] font-semibold tabular-nums">
          {formatRemaining(remaining)}
        </div>
      </div>

      {auction.isViewerLeading && !isOver && (
        <p className="rounded-control bg-good-soft px-3 py-2 text-[13px] text-good">
          {t('auction.youLead')}
        </p>
      )}

      {isOver ? (
        <p className="text-[13px] text-ink-2">{t('auction.closed')}</p>
      ) : isAuthenticated ? (
        auction.canViewerBid ? (
          <BidForm auction={auction} />
        ) : (
          <p className="text-[13px] text-ink-2">{t('auction.ownLot')}</p>
        )
      ) : (
        <button type="button" onClick={onSignIn} className="btn btn-signal w-full py-3">
          {t('auction.signInToBid')}
        </button>
      )}
    </div>
  )
}

function BidForm({ auction }: { auction: AuctionDetails }) {
  const { t } = useTranslation()

  const [amount, setAmount] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Мінімум міг щойно вирости через чужу ставку — підставляємо свіжий.
  const minimum = auction.minimumNextBid

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)

    try {
      await placeBid(auction.listingId, Number(amount))
      setAmount('')
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : t('error.network'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} className="grid gap-2">
      <label className="grid gap-1">
        <span className="text-[11.5px] font-semibold text-ink-2">
          {t('auction.yourMaximum')}
        </span>
        <input
          type="number"
          inputMode="numeric"
          value={amount}
          min={minimum}
          step={auction.bidStep}
          placeholder={String(minimum)}
          onChange={(event) => setAmount(event.target.value)}
          required
          className="control font-mono tabular-nums placeholder:font-sans placeholder:text-ink-3"
        />
      </label>

      {/* Дві готові суми: перебити мінімально або з запасом на один крок. */}
      <div className="flex gap-2">
        {[minimum, minimum + auction.bidStep].map((suggested) => (
          <button
            key={suggested}
            type="button"
            onClick={() => setAmount(String(suggested))}
            className="btn flex-1 py-1.5 text-[13px]"
          >
            {formatPrice(suggested, auction.currency)}
          </button>
        ))}
      </div>

      <p className="text-[12px] text-ink-3">
        {t('auction.ceilingHint')}
      </p>

      {error && (
        <p className="rounded-control bg-danger-soft px-3 py-2 text-[13px] text-danger">{error}</p>
      )}

      <button type="submit" disabled={busy} className="btn btn-signal w-full py-3">
        {busy ? t('auction.placing') : t('auction.placeBid')}
      </button>
    </form>
  )
}

function History({ bids, currency }: { bids: BidRecord[]; currency: AuctionDetails['currency'] }) {
  const { t } = useTranslation()

  if (bids.length === 0) {
    return (
      <div className="card p-4">
        <h2 className="eyebrow mb-2">{t('auction.history')}</h2>
        <p className="text-[13px] text-ink-2">{t('auction.noBidsYet')}</p>
      </div>
    )
  }

  return (
    <div className="card p-4">
      <h2 className="eyebrow mb-2">{t('auction.historyCount', { count: bids.length })}</h2>

      <ol className="grid">
        {bids.map((bid, index) => (
          <li
            key={bid.id}
            className={`flex items-center justify-between gap-3 border-b border-line py-2 last:border-0 ${
              index === 0 ? 'font-semibold' : ''
            }`}
          >
            <div className="min-w-0">
              <div className="truncate text-[13.5px]">{bid.bidderName}</div>
              <div className="text-[11.5px] text-ink-3">
                {formatDateTime(bid.createdAt)}
              </div>
            </div>

            <div className="flex shrink-0 items-center gap-2">
              {/* Бейдж потрібен, щоб не здавалося, ніби ціна стрибнула сама. */}
              {bid.isAutomatic && <span className="pill">{t('auction.automatic')}</span>}
              <span className={`font-mono tabular-nums ${index === 0 ? 'text-signal' : ''}`}>
                {formatPrice(bid.amount, currency)}
              </span>
            </div>
          </li>
        ))}
      </ol>
    </div>
  )
}
