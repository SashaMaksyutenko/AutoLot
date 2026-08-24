import type { ListingSummary } from '../../api/catalog'
import { useAttributeLabels } from '../../api/useAttributeLabels'
import { formatMileage, formatPrice } from '../../format'

/**
 * Рядок видачі. Лот з торгами відрізняється від звичайного оголошення
 * бурштиновою смугою та іншим блоком ціни — саме тим, чим AutoLot
 * відрізняється від звичайного класифайду.
 */
export function ListingRow({ listing }: { listing: ListingSummary }) {
  const isAuction = listing.type === 'Auction'
  const labelOf = useAttributeLabels()

  return (
    <article
      className={`flex gap-4 rounded-md border bg-surface p-3 ${
        isAuction ? 'border-lot-line border-l-[3px] border-l-lot' : 'border-line'
      }`}
    >
      <Photo listing={listing} isAuction={isAuction} />

      <div className="flex min-w-0 flex-grow flex-col gap-2 py-0.5">
        <header className="flex items-start justify-between gap-5">
          <div className="min-w-0">
            <h3 className="truncate text-[17px] font-semibold tracking-tight">
              {listing.make} {listing.model}
            </h3>
            <p className="mt-0.5 text-[13px] text-subtle">
              {listing.year} · {listing.cityName}
            </p>
          </div>

          <div className="shrink-0 text-right">
            {isAuction && (
              <div className="text-[11px] uppercase tracking-wide text-lot-ink">
                Стартова ціна
              </div>
            )}
            <div
              className={`font-mono text-[21px] font-bold tracking-tight ${
                isAuction ? 'text-lot-ink' : ''
              }`}
            >
              {formatPrice(listing.price, listing.currency)}
            </div>
            {listing.currency !== 'Uah' && (
              <div className="mt-0.5 font-mono text-xs text-subtle">
                {formatPrice(listing.priceUah, 'Uah')}
              </div>
            )}
          </div>
        </header>

        <dl className="grid grid-cols-4 gap-2 border-y border-[#eceff2] py-2">
          <Spec label="Пробіг" value={formatMileage(listing.mileage)} mono />
          <Spec label="Пальне" value={labelOf('fuelTypes', listing.fuelType)} />
          <Spec label="Коробка" value={labelOf('transmissions', listing.transmission)} />
          <Spec label="Рік" value={String(listing.year)} mono />
        </dl>

        <footer className="flex items-center justify-between gap-4">
          <span className="text-[13px] text-subtle">
            {isAuction ? 'Торги ще не почалися' : 'Фіксована ціна'}
          </span>

          {isAuction && (
            <span className="rounded-sm bg-lot px-4 py-1.5 text-[13px] font-semibold text-white">
              До лота
            </span>
          )}
        </footer>
      </div>
    </article>
  )
}

function Photo({ listing, isAuction }: { listing: ListingSummary; isAuction: boolean }) {
  return (
    <div className="relative h-[156px] w-[232px] shrink-0 overflow-hidden rounded-sm bg-[#2f3e52]">
      {listing.primaryPhotoPath ? (
        <img
          src={`/media/${listing.primaryPhotoPath}`}
          alt={`${listing.make} ${listing.model}`}
          className="h-full w-full object-cover"
          loading="lazy"
        />
      ) : (
        <div className="flex h-full w-full flex-col items-center justify-center gap-1">
          <span className="text-[15px] font-semibold tracking-wide text-white">
            {listing.make.toUpperCase()}
          </span>
          <span className="text-xs text-[#d6dee6]">
            {listing.model} · {listing.year}
          </span>
        </div>
      )}

      {isAuction && (
        <span className="absolute top-2 left-2 rounded-sm bg-lot px-2 py-0.5 text-[11px] font-bold tracking-wider text-white uppercase">
          Торги
        </span>
      )}
    </div>
  )
}

function Spec({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="min-w-0">
      <dt className="text-[11px] text-faint">{label}</dt>
      <dd className={`mt-0.5 truncate text-sm ${mono ? 'font-mono' : ''}`}>{value}</dd>
    </div>
  )
}
