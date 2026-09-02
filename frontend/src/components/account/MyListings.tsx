import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  archiveListing,
  deleteDraft,
  fetchMyListings,
  submitForModeration,
} from '../../api/myListings'
import { ApiError } from '../../api/client'
import type { ListingSummary } from '../../api/catalog'
import { formatMileage, formatPrice, plural } from '../../format'
import { ReviewPrompt } from './ReviewPrompt'
import { SoldForm } from './SoldForm'

/**
 * «Мої оголошення» — робоче місце продавця.
 *
 * Показуємо всі статуси разом, а не лише опубліковані: увага господаря
 * потрібна саме чернеткам і відхиленим. Відсортоване з бекенду — найновіші
 * зверху.
 */
export function MyListings() {
  const listings = useQuery({
    queryKey: ['my-listings'],
    queryFn: ({ signal }) => fetchMyListings(undefined, signal),
  })

  if (listings.isPending) {
    return <section className="card p-6 text-sm text-ink-2">Завантажуємо…</section>
  }

  if (listings.isError) {
    return (
      <section className="card p-6 text-sm text-danger">Не вдалося отримати оголошення.</section>
    )
  }

  const items = listings.data ?? []

  return (
    <section className="grid gap-3">
      <div className="flex items-baseline justify-between gap-3">
        <h2 className="font-display text-[19px] font-bold">Мої оголошення</h2>
        <span className="text-[12.5px] text-ink-3">
          {items.length} {plural(items.length, 'оголошення', 'оголошення', 'оголошень')}
        </span>
      </div>

      {items.length === 0 ? (
        <p className="card p-8 text-center text-sm text-ink-2">
          Оголошень ще немає. Коли подасте перше, воно з'явиться тут.
        </p>
      ) : (
        items.map((listing) => <ListingRow key={listing.id} listing={listing} />)
      )}
    </section>
  )
}

function ListingRow({ listing }: { listing: ListingSummary }) {
  const queryClient = useQueryClient()
  const [selling, setSelling] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['my-listings'] })
    void queryClient.invalidateQueries({ queryKey: ['listing', listing.id] })
  }

  const act = useMutation({
    mutationFn: (action: 'submit' | 'archive' | 'delete') => {
      if (action === 'submit') return submitForModeration(listing.id)
      if (action === 'archive') return archiveListing(listing.id)

      return deleteDraft(listing.id)
    },
    onSuccess: refresh,
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : 'Не вдалося виконати дію.'),
  })

  // Що можна зробити, вирішує статус — ті самі правила, що й у домені.
  // Тут вони лише малюються: сервер перевіряє їх заново й не покладається
  // на те, які кнопки показав браузер.
  const canSubmit = listing.status === 'Draft' || listing.status === 'Rejected'
  const canSell = listing.status === 'Active'
  const canArchive = listing.status !== 'Draft' && listing.status !== 'Archived'
  const canDelete = listing.status === 'Draft'

  return (
    <article className="card grid gap-3 p-3">
      <div className="flex flex-wrap items-start gap-3">
        <Thumbnail listing={listing} />

        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <StatusPill status={listing.status} />
            {listing.type === 'Auction' && <span className="pill pill-accent">Торги</span>}
          </div>

          <Link
            to={`/listing/${listing.id}`}
            className="font-display text-[15.5px] font-semibold hover:text-accent"
          >
            {listing.make} {listing.model}
          </Link>

          <p className="truncate text-[12.5px] text-ink-2">
            {[listing.year, formatMileage(listing.mileage), listing.cityName].join(' · ')}
          </p>

          <p className="font-mono text-[13px] tabular-nums">
            {formatPrice(listing.price, listing.currency)}
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          {canSubmit && (
            <button
              type="button"
              onClick={() => act.mutate('submit')}
              disabled={act.isPending}
              className="btn btn-primary"
            >
              На модерацію
            </button>
          )}

          {canSell && (
            <button
              type="button"
              onClick={() => setSelling((open) => !open)}
              disabled={act.isPending}
              className="btn btn-primary"
            >
              Продано
            </button>
          )}

          {canArchive && (
            <button
              type="button"
              onClick={() => act.mutate('archive')}
              disabled={act.isPending}
              className="btn"
            >
              В архів
            </button>
          )}

          {/* Видаляють лише чернетку — решта має лишати слід. */}
          {canDelete && (
            <button
              type="button"
              onClick={() => act.mutate('delete')}
              disabled={act.isPending}
              className="btn"
            >
              Видалити
            </button>
          )}
        </div>
      </div>

      {/* Продали — саме час оцінити покупця, поки угода свіжа. */}
      {listing.status === 'Sold' && <ReviewPrompt listingId={listing.id} />}

      {listing.status === 'Rejected' && (
        <p className="rounded-control bg-surface-2 px-2.5 py-2 text-[12.5px] text-ink-2">
          Оголошення знято. Відкрийте його, виправте зауваження й подайте знову.
        </p>
      )}

      {error && <p className="text-[12px] text-danger">{error}</p>}

      {selling && (
        <SoldForm
          listingId={listing.id}
          onDone={() => {
            setSelling(false)
            refresh()
          }}
          onCancel={() => setSelling(false)}
        />
      )}
    </article>
  )
}

/** Статус словами. Кольором виділяємо лише те, що потребує дії господаря. */
function StatusPill({ status }: { status: string }) {
  const labels: Record<string, string> = {
    Draft: 'Чернетка',
    PendingModeration: 'На модерації',
    Active: 'Опубліковано',
    Sold: 'Продано',
    Expired: 'Строк вийшов',
    Rejected: 'Знято',
    Archived: 'В архіві',
  }

  const needsAttention = status === 'Draft' || status === 'Rejected' || status === 'Expired'

  return (
    <span className={needsAttention ? 'pill pill-live' : 'pill'}>{labels[status] ?? status}</span>
  )
}

function Thumbnail({ listing }: { listing: ListingSummary }) {
  if (!listing.primaryPhotoPath) {
    return (
      <span className="grid h-[60px] w-[80px] shrink-0 place-items-center rounded-control border border-line bg-surface-2 text-[11px] text-ink-3">
        без фото
      </span>
    )
  }

  return (
    <img
      src={`/media/${listing.primaryPhotoPath}`}
      alt=""
      className="h-[60px] w-[80px] shrink-0 rounded-control border border-line object-cover"
    />
  )
}
