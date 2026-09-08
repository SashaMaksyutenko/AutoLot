import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { approveListing, fetchModerationQueue, rejectListing } from '../../api/admin'
import { ApiError } from '../../api/client'
import type { ListingSummary } from '../../api/catalog'
import { formatMileage, formatPrice } from '../../format'
import { useTranslation } from '../../i18n/useTranslation'

/**
 * Черга модерації. Механіка на бекенді працювала від четвертого пункту, але
 * розбирати чергу доводилося через curl — робочого місця для неї не було.
 */
export function ModerationQueuePage() {
  const { t, tPlural } = useTranslation()

  const queryClient = useQueryClient()

  const queue = useQuery({
    queryKey: ['moderation-queue'],
    queryFn: ({ signal }) => fetchModerationQueue(signal),
  })

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['moderation-queue'] })
    void queryClient.invalidateQueries({ queryKey: ['admin-stats'] })
  }

  return (
    <>
      <div>
        <h1 className="font-display text-[25px] font-bold">{t('queue.title')}</h1>
        <p className="text-[13px] text-ink-2">
          {queue.isPending
            ? t('admin.loading')
            : tPlural('queue.waiting', queue.data?.length ?? 0)}
        </p>
      </div>

      {queue.data?.length === 0 && (
        <p className="card p-10 text-center text-sm text-ink-2">
          {t('queue.empty')}
        </p>
      )}

      <div className="grid gap-3">
        {queue.data?.map((listing) => (
          <QueueRow key={listing.id} listing={listing} onDecided={refresh} />
        ))}
      </div>
    </>
  )
}

function QueueRow({
  listing,
  onDecided,
}: {
  listing: ListingSummary
  onDecided: () => void
}) {
  const { t } = useTranslation()

  const [rejecting, setRejecting] = useState(false)
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string | null>(null)

  const decide = useMutation({
    mutationFn: (decision: 'approve' | 'reject') =>
      decision === 'approve' ? approveListing(listing.id) : rejectListing(listing.id, reason),
    onSuccess: onDecided,
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : t('queue.decisionFailed')),
  })

  return (
    <article className="card grid gap-3 p-3">
      <div className="flex flex-wrap items-start gap-3">
        <Thumbnail listing={listing} />

        <div className="min-w-0 flex-1">
          {/* Модератор має відкрити картку й подивитися фото та опис. */}
          <Link
            to={`/listing/${listing.id}`}
            target="_blank"
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

        <div className="flex gap-2">
          <button
            type="button"
            onClick={() => decide.mutate('approve')}
            disabled={decide.isPending}
            className="btn btn-primary"
          >
            {t('queue.approve')}
          </button>
          <button
            type="button"
            onClick={() => setRejecting((open) => !open)}
            disabled={decide.isPending}
            className="btn"
          >
            {t('queue.reject')}
          </button>
        </div>
      </div>

      {rejecting && (
        <form
          className="grid gap-2 border-t border-line pt-3"
          onSubmit={(event) => {
            event.preventDefault()
            setError(null)
            decide.mutate('reject')
          }}
        >
          <label className="grid gap-1">
            <span className="text-[11.5px] font-semibold text-ink-2">
              {t('queue.reasonLabel')}
            </span>
            <textarea
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              rows={2}
              maxLength={500}
              placeholder={t('queue.reasonPlaceholder')}
              className="control resize-y"
            />
          </label>

          {error && <p className="text-[12px] text-danger">{error}</p>}

          <div className="flex gap-2">
            {/* Без причини не відхиляємо: автор має розуміти, що виправляти. */}
            <button
              type="submit"
              disabled={decide.isPending || reason.trim().length < 5}
              className="btn btn-primary"
            >
              {decide.isPending ? t('queue.saving') : t('queue.rejectWithReason')}
            </button>
            <button type="button" onClick={() => setRejecting(false)} className="btn">
              {t('queue.cancel')}
            </button>
          </div>
        </form>
      )}
    </article>
  )
}

function Thumbnail({ listing }: { listing: ListingSummary }) {
  const { t } = useTranslation()

  if (!listing.primaryPhotoPath) {
    return (
      <span className="grid h-[60px] w-[80px] shrink-0 place-items-center rounded-control border border-line bg-surface-2 text-[11px] text-ink-3">
        {t('queue.noPhoto')}
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
