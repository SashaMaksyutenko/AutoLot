import { Link, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { fetchListing, type CarDetails, type ListingDetails } from '../api/listing'
import { useAttributeLabels } from '../api/useAttributeLabels'
import { Gallery } from '../components/listing/Gallery'
import { formatCount, formatMileage, formatPrice, plural } from '../format'

export function ListingPage() {
  const { id } = useParams()
  const listingId = Number(id)

  const listing = useQuery({
    queryKey: ['listing', listingId],
    queryFn: ({ signal }) => fetchListing(listingId, signal),
    enabled: Number.isInteger(listingId) && listingId > 0,
  })

  if (listing.isPending) {
    return <Notice>Завантажуємо…</Notice>
  }

  if (listing.isError || !listing.data) {
    return (
      <Notice>
        Оголошення не знайдено або воно ще не опубліковане.{' '}
        <Link to="/" className="text-brand hover:underline">
          Повернутися до каталогу
        </Link>
      </Notice>
    )
  }

  return <Loaded listing={listing.data} />
}

function Loaded({ listing }: { listing: ListingDetails }) {
  const labelOf = useAttributeLabels()
  const car = listing.car
  const isAuction = listing.type === 'Auction'

  return (
    <div className="mx-auto flex max-w-[1180px] flex-col gap-5 px-10 pt-6 pb-14">
      <nav className="text-[13px] text-subtle">
        <Link to="/" className="hover:text-brand hover:underline">
          Каталог
        </Link>
        <span className="px-1.5 text-faint">/</span>
        <span>
          {car.make} {car.model}
        </span>
      </nav>

      <div className="flex gap-6">
        <div className="flex min-w-0 flex-grow flex-col gap-5">
          <Gallery photos={listing.photos} alt={`${car.make} ${car.model}`} />

          <Panel title="Характеристики">
            <dl className="grid grid-cols-2 gap-x-10 gap-y-0">
              <Row label="Рік випуску" value={String(car.year)} mono />
              {/* Стан не входить у довідник перелічень — значень лише два. */}
              <Row label="Стан" value={car.condition === 'New' ? 'Новий' : 'Вживаний'} />
              <Row label="Пробіг" value={formatMileage(car.mileage)} mono />
              <Row label="Кузов" value={labelOf('bodyTypes', car.bodyType)} />
              <Row label="Пальне" value={labelOf('fuelTypes', car.fuelType)} />
              <Row label="Коробка" value={labelOf('transmissions', car.transmission)} />
              <Row label="Привід" value={labelOf('driveTypes', car.drivetrain)} />
              <Row label="Колір" value={`${labelOf('colors', car.color)}${car.isMetallic ? ', металік' : ''}`} />
              <Row label="Двигун" value={engineLine(car)} />
              <Row label="Потужність" value={car.enginePower ? `${car.enginePower} к.с.` : null} mono />
              <Row label="Витрата, змішана" value={car.fuelConsumptionCombined ? `${car.fuelConsumptionCombined} л/100 км` : null} mono />
              <Row label="Батарея" value={car.batteryCapacity ? `${car.batteryCapacity} кВт·год` : null} mono />
              <Row label="Запас ходу" value={car.electricRange ? `${car.electricRange} км` : null} mono />
              <Row label="Місць" value={car.seatCount ? String(car.seatCount) : null} mono />
              <Row label="Дверей" value={car.doorCount ? String(car.doorCount) : null} mono />
              <Row label="Екостандарт" value={car.ecologyStandard} />
              <Row label="Власників" value={car.ownerCount ? String(car.ownerCount) : null} mono />
              <Row label="VIN" value={car.vin} mono />
            </dl>
          </Panel>

          <Panel title="Стан та історія">
            <div className="flex flex-wrap gap-2">
              <Fact ok={!car.wasInAccident} text={car.wasInAccident ? 'Був у ДТП' : 'Не був у ДТП'} />
              <Fact ok={car.isCustomsCleared} text={car.isCustomsCleared ? 'Розмитнений' : 'Не розмитнений'} />
              <Fact ok={car.isLocatedInUkraine} text={car.isLocatedInUkraine ? 'В Україні' : 'Під замовлення'} />
              {car.hasServiceBook && <Fact ok text="Сервісна книжка" />}
              {car.isGarageKept && <Fact ok text="Гаражне зберігання" />}
              {car.isOnCredit && <Fact ok={false} text="У кредиті" />}
              {car.importedFromCountry && <Fact ok text={`Пригнаний: ${car.importedFromCountry}`} />}
            </div>
          </Panel>

          {car.features.length > 0 && (
            <Panel title={`Комплектація · ${car.features.length}`}>
              <ul className="grid grid-cols-3 gap-x-6 gap-y-1.5">
                {car.features.map((feature) => (
                  <li key={feature} className="flex items-start gap-2 text-sm">
                    <span className="mt-1.5 h-1 w-1 shrink-0 rounded-full bg-faint" />
                    <span>{feature}</span>
                  </li>
                ))}
              </ul>
            </Panel>
          )}

          <Panel title="Опис">
            <p className="text-sm leading-relaxed whitespace-pre-line text-muted">
              {listing.description}
            </p>
          </Panel>
        </div>

        <aside className="flex w-[320px] shrink-0 flex-col gap-4 self-start">
          <div
            className={`rounded-md border bg-surface p-5 ${
              isAuction ? 'border-lot-line border-t-[3px] border-t-lot' : 'border-line'
            }`}
          >
            <h1 className="text-xl font-semibold tracking-tight">
              {car.make} {car.model}
            </h1>
            <p className="mt-1 text-[13px] text-subtle">
              {car.generation ? `${car.generation} · ` : ''}
              {car.year}
            </p>

            <div className="mt-4">
              {isAuction && (
                <div className="text-[11px] tracking-wide text-lot-ink uppercase">Стартова ціна</div>
              )}
              <div
                className={`font-mono text-[30px] font-bold tracking-tight ${
                  isAuction ? 'text-lot-ink' : ''
                }`}
              >
                {formatPrice(listing.price, listing.currency)}
              </div>
              {listing.currency !== 'Uah' && (
                <div className="mt-0.5 font-mono text-sm text-subtle">
                  {formatPrice(listing.priceUah, 'Uah')}
                </div>
              )}
            </div>

            {(listing.isNegotiable || listing.acceptsTrade || listing.isUrgent) && (
              <div className="mt-3 flex flex-wrap gap-1.5">
                {listing.isNegotiable && <Tag text="Торг доречний" />}
                {listing.acceptsTrade && <Tag text="Розглядаю обмін" />}
                {listing.isUrgent && <Tag text="Терміновий продаж" />}
              </div>
            )}

            <button
              type="button"
              className={`mt-5 h-11 w-full rounded-sm text-sm font-semibold text-white ${
                isAuction ? 'bg-lot' : 'bg-brand'
              }`}
            >
              {isAuction ? 'Перейти до торгів' : 'Показати телефон'}
            </button>
          </div>

          <div className="rounded-md border border-line bg-surface p-5">
            <div className="text-xs font-semibold tracking-wider text-muted uppercase">Продавець</div>
            <div className="mt-2.5 text-[15px] font-semibold">{listing.seller.displayName}</div>
            <div className="mt-0.5 text-[13px] text-subtle">
              {listing.seller.accountType === 'Dealer' ? 'Автосалон' : 'Приватна особа'}
            </div>

            {listing.location && (
              <div className="mt-3 border-t border-line pt-3 text-[13px] text-muted">
                {listing.location.cityName}
                {listing.location.cityDistrictName ? `, ${listing.location.cityDistrictName}` : ''}
                <div className="text-subtle">{listing.location.regionName}</div>
              </div>
            )}
          </div>

          <div className="px-1 text-[13px] text-subtle">
            {formatCount(listing.viewCount)}{' '}
            {plural(listing.viewCount, 'перегляд', 'перегляди', 'переглядів')}
          </div>
        </aside>
      </div>
    </div>
  )
}

/** Збирає рядок про двигун із того, що заповнене: об'єм є не в кожного авто. */
function engineLine(car: CarDetails): string | null {
  const parts = [car.engineVolume ? `${car.engineVolume} л` : null].filter(Boolean)

  return parts.length > 0 ? parts.join(' · ') : null
}

function Panel({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="rounded-md border border-line bg-surface p-5">
      <h2 className="mb-3.5 text-xs font-semibold tracking-wider text-muted uppercase">{title}</h2>
      {children}
    </section>
  )
}

/** Порожні характеристики не показуємо взагалі — рядок «—» лише засмічує таблицю. */
function Row({ label, value, mono }: { label: string; value: string | null; mono?: boolean }) {
  if (!value) return null

  return (
    <div className="flex items-baseline justify-between gap-4 border-b border-[#eceff2] py-2 last:border-0">
      <dt className="text-[13px] text-subtle">{label}</dt>
      <dd className={`text-sm font-medium ${mono ? 'font-mono' : ''}`}>{value}</dd>
    </div>
  )
}

function Fact({ ok, text }: { ok: boolean; text: string }) {
  return (
    <span
      className={`rounded-sm px-2.5 py-1 text-[13px] ${
        ok ? 'bg-ok-soft text-ok-ink' : 'bg-warn-soft text-warn-ink'
      }`}
    >
      {text}
    </span>
  )
}

function Tag({ text }: { text: string }) {
  return <span className="rounded-sm bg-brand-soft px-2 py-1 text-[11px] font-medium text-brand">{text}</span>
}

function Notice({ children }: { children: React.ReactNode }) {
  return (
    <div className="mx-auto max-w-[1180px] px-10 py-16">
      <p className="rounded-md border border-line bg-surface p-10 text-center text-sm text-subtle">
        {children}
      </p>
    </div>
  )
}
