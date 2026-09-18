import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  archiveListing,
  deleteDraft,
  fetchMyListings,
  changePrice,
  submitForModeration,
} from '../../api/myListings'
import { ApiError } from '../../api/client'
import type { ListingSummary } from '../../api/catalog'
import { formatMileage, formatPrice } from '../../format'
import { useTranslation } from '../../i18n/useTranslation'
import type { MessageKey } from '../../i18n/messages'
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
  const { t, tPlural } = useTranslation()

  const listings = useQuery({
    queryKey: ['my-listings'],
    queryFn: ({ signal }) => fetchMyListings(undefined, signal),
  })

  if (listings.isPending) {
    return <section className="card p-6 text-sm text-ink-2">{t('my.loading')}</section>
  }

  if (listings.isError) {
    return (
      <section className="card p-6 text-sm text-danger">{t('my.listingsFailed')}</section>
    )
  }

  const items = listings.data ?? []

  return (
    <section className="grid gap-3">
      <div className="flex items-baseline justify-between gap-3">
        <h2 className="font-display text-[19px] font-bold">{t('my.listings')}</h2>
        <span className="text-[12.5px] text-ink-3">
          {tPlural('my.count', items.length)}
        </span>
      </div>

      {items.length === 0 ? (
        <p className="card p-8 text-center text-sm text-ink-2">
          {t('my.listingsEmpty')}
        </p>
      ) : (
        items.map((listing) => <ListingRow key={listing.id} listing={listing} />)
      )}
    </section>
  )
}

function ListingRow({ listing }: { listing: ListingSummary }) {
  const { t } = useTranslation()

  const queryClient = useQueryClient()
  const [selling, setSelling] = useState(false)
  const [repricing, setRepricing] = useState(false)
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
      setError(caught instanceof ApiError ? caught.message : t('my.actionFailed')),
  })

  // Що можна зробити, вирішує статус — ті самі правила, що й у домені.
  // Тут вони лише малюються: сервер перевіряє їх заново й не покладається
  // на те, які кнопки показав браузер.
  // Сервер дозволяє правити рівно ці два стани — див. Listing.IsEditable.
  const canEdit = listing.status === 'Draft' || listing.status === 'Rejected'
  const canSubmit = canEdit
  const canSell = listing.status === 'Active'

  // Ціну міняють лише в опублікованому, і лише там, де її задає продавець:
  // у торгах ціну ведуть ставки.
  const canReprice = listing.status === 'Active' && listing.type !== 'Auction'
  const canArchive = listing.status !== 'Draft' && listing.status !== 'Archived'
  const canDelete = listing.status === 'Draft'

  return (
    <article className="card grid gap-3 p-3">
      <div className="flex flex-wrap items-start gap-3">
        <Thumbnail listing={listing} />

        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <StatusPill status={listing.status} />
            {listing.type === 'Auction' && <span className="pill pill-accent">{t('my.auction')}</span>}
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
          {/*
            Редагувати можна те саме, що дозволяє сервер: чернетку й зняте.
            Опубліковане правити не можна — покупці вже бачили інші умови.
          */}
          {canEdit && (
            <Link to={`/listing/${listing.id}/edit`} className="btn">
              {t('my.edit')}
            </Link>
          )}

          {canSubmit && (
            <button
              type="button"
              onClick={() => act.mutate('submit')}
              disabled={act.isPending}
              className="btn btn-primary"
            >
              {t('my.submit')}
            </button>
          )}

          {canReprice && (
            <button
              type="button"
              onClick={() => setRepricing((open) => !open)}
              disabled={act.isPending}
              className="btn"
            >
              {t('priceHistory.changePrice')}
            </button>
          )}

          {canSell && (
            <button
              type="button"
              onClick={() => setSelling((open) => !open)}
              disabled={act.isPending}
              className="btn btn-primary"
            >
              {t('my.sold')}
            </button>
          )}

          {canArchive && (
            <button
              type="button"
              onClick={() => act.mutate('archive')}
              disabled={act.isPending}
              className="btn"
            >
              {t('my.archive')}
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
              {t('my.delete')}
            </button>
          )}
        </div>
      </div>

      {/* Продали — саме час оцінити покупця, поки угода свіжа. */}
      {listing.status === 'Sold' && <ReviewPrompt listingId={listing.id} />}

      {listing.status === 'Rejected' && (
        <p className="rounded-control bg-surface-2 px-2.5 py-2 text-[12.5px] text-ink-2">
          {t('my.rejectedNote')}
        </p>
      )}

      {error && <p className="text-[12px] text-danger">{error}</p>}

      {repricing && (
        <PriceForm
          listing={listing}
          onDone={() => {
            setRepricing(false)
            refresh()
          }}
        />
      )}

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
  const { t } = useTranslation()

  const labels: Record<string, MessageKey> = {
    Draft: 'status.Draft',
    PendingModeration: 'status.PendingModeration',
    Active: 'status.Active',
    Sold: 'status.Sold',
    Expired: 'status.Expired',
    Rejected: 'status.Rejected',
    Archived: 'status.Archived',
  }

  const needsAttention = status === 'Draft' || status === 'Rejected' || status === 'Expired'

  return (
    <span className={needsAttention ? 'pill pill-live' : 'pill'}>
      {labels[status] ? t(labels[status]) : status}
    </span>
  )
}

function Thumbnail({ listing }: { listing: ListingSummary }) {
  const { t } = useTranslation()

  if (!listing.primaryPhotoPath) {
    return (
      <span className="grid h-[60px] w-[80px] shrink-0 place-items-center rounded-control border border-line bg-surface-2 text-[11px] text-ink-3">
        {t('my.noPhoto')}
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

/**
 * Зміна ціни опублікованого оголошення.
 *
 * Валюту не питаємо: міняти її разом із ціною означало б зробити історію
 * нечитабельною («було 5000, стало 200000» — це подорожчання чи зміна
 * валюти?). Кому справді треба інша валюта, зніме оголошення й подасть
 * наново.
 */
function PriceForm({ listing, onDone }: { listing: ListingSummary; onDone: () => void }) {
  const { t } = useTranslation()
  const [price, setPrice] = useState(String(listing.price))
  const [error, setError] = useState<string | null>(null)

  const save = useMutation({
    mutationFn: () => changePrice(listing.id, Number(price), listing.currency),
    onSuccess: onDone,
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : t('my.actionFailed')),
  })

  return (
    <div className="grid gap-2 rounded-control bg-surface-2 p-2.5">
      <label className="grid gap-1 text-[12.5px] text-ink-2">
        {t('priceHistory.newPrice')}
        <div className="flex flex-wrap items-center gap-2">
          <input
            value={price}
            onChange={(event) => setPrice(event.target.value)}
            inputMode="numeric"
            className="control w-40 font-mono tabular-nums"
          />
          <span className="font-mono text-[13px]">{listing.currency}</span>
        </div>
      </label>

      <div className="flex flex-wrap gap-2">
        <button
          type="button"
          onClick={() => save.mutate()}
          disabled={save.isPending || Number(price) <= 0}
          className="btn btn-primary"
        >
          {t('priceHistory.save')}
        </button>

        <button type="button" onClick={onDone} className="btn">
          {t('priceHistory.cancel')}
        </button>
      </div>

      {error && <p className="text-[12px] text-danger">{error}</p>}
    </div>
  )
}
