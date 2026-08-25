import { Link } from 'react-router-dom'
import type { ListingSummary } from '../../api/catalog'
import { useAttributeLabels } from '../../api/useAttributeLabels'
import { formatMileage, formatPrice } from '../../format'
import { FavoriteButton } from '../FavoriteButton'

/**
 * Картка оголошення у видачі. Лот з торгами відрізняється сигнальною
 * «пігулкою» на фото та підписом «стартова ціна» — саме тим, чим AutoLot
 * відрізняється від звичайного класифайду.
 */
export function ListingCard({ listing }: { listing: ListingSummary }) {
  const isAuction = listing.type === 'Auction'
  const labelOf = useAttributeLabels()

  return (
    <Link to={`/listing/${listing.id}`} className="block">
      {/*
        transition + hover:-translate-y-px — картка ледь підводиться під
        курсором. Дрібниця, але вона показує, що на неї можна натиснути.
      */}
      <article className="card grid gap-2.5 p-2.5 transition hover:-translate-y-px hover:border-ink-3">
        <Photo listing={listing} isAuction={isAuction} />

        <div className="grid gap-[7px] px-0.5">
          <h3 className="font-display truncate text-[15.5px] font-semibold">
            {listing.make} {listing.model}
          </h3>

          <p className="truncate text-[12.5px] text-ink-2">
            {[
              listing.year,
              formatMileage(listing.mileage),
              labelOf('fuelTypes', listing.fuelType),
              labelOf('transmissions', listing.transmission),
              listing.cityName,
            ].join(' · ')}
          </p>

          <div className="flex items-end justify-between gap-2 border-t border-line pt-2">
            <div>
              {isAuction && <div className="eyebrow">Стартова ціна</div>}
              <div
                className={`font-display text-[19px] font-bold tabular-nums ${
                  isAuction ? 'text-signal' : ''
                }`}
              >
                {formatPrice(listing.price, listing.currency)}
              </div>
              {listing.currency !== 'Uah' && (
                <div className="font-mono text-[11.5px] text-ink-3 tabular-nums">
                  {formatPrice(listing.priceUah, 'Uah')}
                </div>
              )}
            </div>

            <span className="pill">{isAuction ? 'До лота' : 'Купити зараз'}</span>
          </div>
        </div>
      </article>
    </Link>
  )
}

function Photo({ listing, isAuction }: { listing: ListingSummary; isAuction: boolean }) {
  return (
    <div className="relative aspect-[4/3] overflow-hidden rounded-control border border-line bg-surface-2">
      {listing.primaryPhotoPath ? (
        <img
          src={`/media/${listing.primaryPhotoPath}`}
          alt={`${listing.make} ${listing.model}`}
          className="h-full w-full object-cover"
          loading="lazy"
        />
      ) : (
        // Заглушка замість фото: назва марки на м'якому градієнті. Виглядає
        // як частина оформлення, а не як «зображення не завантажилося».
        <div className="flex h-full w-full flex-col items-center justify-center gap-1 bg-radial-[at_50%_12%] from-surface-2 to-surface-3">
          <span className="font-display text-[15px] font-bold tracking-wide text-ink-3">
            {listing.make.toUpperCase()}
          </span>
          <span className="text-xs text-ink-3">
            {listing.model} · {listing.year}
          </span>
        </div>
      )}

      {isAuction && (
        <div className="absolute top-2 left-2">
          <span className="pill pill-live">
            <i className="dot" />
            Торги
          </span>
        </div>
      )}

      <div className="absolute top-2 right-2">
        <FavoriteButton listingId={listing.id} isFavorite={listing.isFavorite} />
      </div>
    </div>
  )
}
