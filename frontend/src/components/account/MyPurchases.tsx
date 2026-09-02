import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { fetchPurchases } from '../../api/myListings'
import type { ListingSummary } from '../../api/catalog'
import { formatMileage, formatPrice, plural } from '../../format'
import { ReviewPrompt } from './ReviewPrompt'

/**
 * «Мої покупки» — дзеркало «моїх оголошень» для другого боку угоди.
 *
 * Головна причина, чому цей список існує: покупцеві не було де побачити
 * своє куплене авто, а отже — і де лишити відгук про продавця. Продавець
 * бачив продане у своїх оголошеннях, покупець не бачив ніде.
 *
 * Порожній список не показуємо взагалі: більшість людей нічого тут не
 * купувала, і постійний блок «покупок немає» лише захаращував би кабінет.
 */
export function MyPurchases() {
  const purchases = useQuery({
    queryKey: ['my-purchases'],
    queryFn: ({ signal }) => fetchPurchases(signal),
  })

  const items = purchases.data ?? []

  if (purchases.isPending || items.length === 0) {
    return null
  }

  return (
    <section className="grid gap-3">
      <div className="flex items-baseline justify-between gap-3">
        <h2 className="font-display text-[19px] font-bold">Мої покупки</h2>
        <span className="text-[12.5px] text-ink-3">
          {items.length} {plural(items.length, 'авто', 'авто', 'авто')}
        </span>
      </div>

      {items.map((listing) => (
        <PurchaseRow key={listing.id} listing={listing} />
      ))}
    </section>
  )
}

function PurchaseRow({ listing }: { listing: ListingSummary }) {
  return (
    <article className="card grid gap-2 p-3">
      <div className="flex flex-wrap items-start gap-3">
        <Thumbnail listing={listing} />

        <div className="min-w-0 flex-1">
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
      </div>

      <ReviewPrompt listingId={listing.id} />
    </article>
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
