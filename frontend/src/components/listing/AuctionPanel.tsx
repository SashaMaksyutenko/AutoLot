import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  fetchAuction,
  fetchBidHistory,
  placeBid,
  type AuctionDetails,
  type AuctionUpdate,
  type BidRecord,
} from '../../api/auction'
import { watchAuction } from '../../api/auctionHub'
import { ApiError } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { openSignIn } from '../../auth/signInPrompt'
import { formatPrice, plural } from '../../format'
import { formatRemaining, useCountdown } from './useCountdown'

/** Менше за стільки до кінця — торги вважаємо гарячими й підсвічуємо таймер. */
const UrgentMilliseconds = 10 * 60 * 1000

export function AuctionPanel({ listingId }: { listingId: number }) {
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

  useEffect(() => {
    return watchAuction(listingId, (update: AuctionUpdate) => {
      setAuction((current) => (current ? merge(current, update, auth.user?.id) : current))

      // Нові рядки приходять найновішим першим — саме так вони й лежать
      // у списку, тож просто додаємо їх на початок.
      setHistory((current) => [...update.newBids, ...current])
    })
  }, [listingId, auth.user?.id])

  if (initial.isPending) {
    return <div className="card p-4 text-sm text-ink-2">Завантажуємо торги…</div>
  }

  if (!auction) {
    return null
  }

  return (
    <>
      <Panel auction={auction} onSignIn={openSignIn} isAuthenticated={auth.user !== null} />
      <History bids={history} currency={auction.currency} />
    </>
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
  const remaining = useCountdown(auction.endsAt, auction.serverTime)
  const isOver = remaining <= 0 || auction.status !== 'Active'

  return (
    <div className="card grid gap-3 border-signal p-4">
      <div className="flex items-center justify-between gap-2">
        <span className="pill pill-live">
          <i className="dot" />
          Торги
        </span>
        {auction.hasReserve ? (
          <span className={`pill ${auction.isReserveMet ? 'pill-good' : 'pill-danger'}`}>
            {auction.isReserveMet ? 'Резерв досягнуто' : 'Резерв не досягнуто'}
          </span>
        ) : (
          <span className="pill pill-good">Без резерву</span>
        )}
      </div>

      <div>
        <div className="eyebrow">{auction.bidCount > 0 ? 'Поточна ставка' : 'Стартова ціна'}</div>
        <div className="font-display text-[30px] leading-tight font-bold text-signal tabular-nums">
          {formatPrice(auction.currentPrice, auction.currency)}
        </div>
        <div className="text-[13px] text-ink-2">
          {auction.bidCount} {plural(auction.bidCount, 'ставка', 'ставки', 'ставок')}
          {auction.leaderName ? ` · попереду ${auction.leaderName}` : ''}
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
        <div className="eyebrow">{isOver ? 'Завершено' : 'До завершення'}</div>
        <div className="font-mono text-[22px] font-semibold tabular-nums">
          {formatRemaining(remaining)}
        </div>
      </div>

      {auction.isViewerLeading && !isOver && (
        <p className="rounded-control bg-good-soft px-3 py-2 text-[13px] text-good">
          Ви попереду. Автоставка підніматиме ціну за вас, поки вистачає вашої стелі.
        </p>
      )}

      {isOver ? (
        <p className="text-[13px] text-ink-2">Ставки більше не приймаються.</p>
      ) : isAuthenticated ? (
        auction.canViewerBid ? (
          <BidForm auction={auction} />
        ) : (
          <p className="text-[13px] text-ink-2">На власний лот ставити не можна.</p>
        )
      ) : (
        <button type="button" onClick={onSignIn} className="btn btn-signal w-full py-3">
          Увійти, щоб зробити ставку
        </button>
      )}
    </div>
  )
}

function BidForm({ auction }: { auction: AuctionDetails }) {
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
      setError(caught instanceof ApiError ? caught.message : 'Не вдалося зв’язатися з сервером.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} className="grid gap-2">
      <label className="grid gap-1">
        <span className="text-[11.5px] font-semibold text-ink-2">
          Ваша максимальна ставка
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
        Це стеля, а не сума платежу: система поставить рівно стільки, скільки потрібно, щоб
        вести, і підніматиме сама, поки вашої стелі вистачає.
      </p>

      {error && (
        <p className="rounded-control bg-danger-soft px-3 py-2 text-[13px] text-danger">{error}</p>
      )}

      <button type="submit" disabled={busy} className="btn btn-signal w-full py-3">
        {busy ? 'Хвилинку…' : 'Поставити'}
      </button>
    </form>
  )
}

function History({ bids, currency }: { bids: BidRecord[]; currency: AuctionDetails['currency'] }) {
  if (bids.length === 0) {
    return (
      <div className="card p-4">
        <h2 className="eyebrow mb-2">Історія ставок</h2>
        <p className="text-[13px] text-ink-2">Ставок ще немає. Ваша може стати першою.</p>
      </div>
    )
  }

  return (
    <div className="card p-4">
      <h2 className="eyebrow mb-2">Історія ставок · {bids.length}</h2>

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
                {new Date(bid.createdAt).toLocaleString('uk-UA')}
              </div>
            </div>

            <div className="flex shrink-0 items-center gap-2">
              {/* Бейдж потрібен, щоб не здавалося, ніби ціна стрибнула сама. */}
              {bid.isAutomatic && <span className="pill">авто</span>}
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
