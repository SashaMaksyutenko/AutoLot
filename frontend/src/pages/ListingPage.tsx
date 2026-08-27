import { Link, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { fetchListing, type ListingDetails } from '../api/listing'
import { useAttributeLabels } from '../api/useAttributeLabels'
import { useAuth } from '../auth/useAuth'
import { FavoriteButton } from '../components/FavoriteButton'
import { AuctionPanel } from '../components/listing/AuctionPanel'
import { Gallery } from '../components/listing/Gallery'
import { Questions } from '../components/listing/Questions'
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
        <Link to="/" className="text-accent hover:underline">
          Повернутися до каталогу
        </Link>
      </Notice>
    )
  }

  return <Loaded listing={listing.data} />
}

function Loaded({ listing }: { listing: ListingDetails }) {
  const auth = useAuth()
  const labelOf = useAttributeLabels()
  const car = listing.car
  const isAuction = listing.type === 'Auction'

  return (
    <div className="wrap grid gap-4 py-[26px]">
      <nav className="text-[13px] text-ink-2">
        <Link to="/" className="hover:text-accent hover:underline">
          Каталог
        </Link>
        <span className="px-1.5 text-ink-3">/</span>
        <span>
          {car.make} {car.model}
        </span>
      </nav>

      <div className="grid items-start gap-[26px] lg:grid-cols-[minmax(0,1.5fr)_minmax(0,1fr)]">
        <div className="grid min-w-0 gap-5">
          <Gallery photos={listing.photos} alt={`${car.make} ${car.model}`} />

          {/*
            Шість головних характеристик у сітці 3×2. Саме шість, бо вони є
            в кожного авто: жодна клітинка не лишиться порожньою, і сітка
            не розсиплеться. Решта, яку заповнюють не всі, — списком нижче.
          */}
          <KeySpecs
            items={[
              { label: 'Рік випуску', value: String(car.year) },
              { label: 'Пробіг', value: formatMileage(car.mileage) },
              { label: 'Кузов', value: labelOf('bodyTypes', car.bodyType) },
              { label: 'Пальне', value: labelOf('fuelTypes', car.fuelType) },
              { label: 'Коробка', value: labelOf('transmissions', car.transmission) },
              { label: 'Привід', value: labelOf('driveTypes', car.drivetrain) },
            ]}
          />

          <Panel title="Технічні дані">
            <dl className="grid gap-x-10 sm:grid-cols-2">
              {/* Стан не входить у довідник перелічень — значень лише два. */}
              <Row label="Стан" value={car.condition === 'New' ? 'Новий' : 'Вживаний'} />
              <Row
                label="Колір"
                value={`${labelOf('colors', car.color)}${car.isMetallic ? ', металік' : ''}`}
              />
              <Row label="Двигун" value={car.engineVolume ? `${car.engineVolume} л` : null} mono />
              <Row
                label="Потужність"
                value={car.enginePower ? `${car.enginePower} к.с.` : null}
                mono
              />
              <Row
                label="Витрата, змішана"
                value={
                  car.fuelConsumptionCombined ? `${car.fuelConsumptionCombined} л/100 км` : null
                }
                mono
              />
              <Row
                label="Батарея"
                value={car.batteryCapacity ? `${car.batteryCapacity} кВт·год` : null}
                mono
              />
              <Row
                label="Запас ходу"
                value={car.electricRange ? `${car.electricRange} км` : null}
                mono
              />
              <Row label="Місць" value={car.seatCount ? String(car.seatCount) : null} mono />
              <Row label="Дверей" value={car.doorCount ? String(car.doorCount) : null} mono />
              <Row label="Екостандарт" value={car.ecologyStandard} />
              <Row label="Власників" value={car.ownerCount ? String(car.ownerCount) : null} mono />
              <Row label="VIN" value={car.vin} mono />
            </dl>
          </Panel>

          <Panel title="Стан та історія">
            <div className="flex flex-wrap gap-1.5">
              <Fact ok={!car.wasInAccident} text={car.wasInAccident ? 'Був у ДТП' : 'Не був у ДТП'} />
              <Fact
                ok={car.isCustomsCleared}
                text={car.isCustomsCleared ? 'Розмитнений' : 'Не розмитнений'}
              />
              <Fact
                ok={car.isLocatedInUkraine}
                text={car.isLocatedInUkraine ? 'В Україні' : 'Під замовлення'}
              />
              {car.hasServiceBook && <Fact ok text="Сервісна книжка" />}
              {car.isGarageKept && <Fact ok text="Гаражне зберігання" />}
              {car.isOnCredit && <Fact ok={false} text="У кредиті" />}
              {car.importedFromCountry && <Fact ok text={`Пригнаний: ${car.importedFromCountry}`} />}
            </div>
          </Panel>

          {car.features.length > 0 && (
            <Panel title={`Комплектація · ${car.features.length}`}>
              <ul className="grid gap-x-6 gap-y-1.5 sm:grid-cols-2 lg:grid-cols-3">
                {car.features.map((feature) => (
                  <li key={feature} className="flex items-start gap-2 text-[13.5px]">
                    <span className="mt-2 h-1 w-1 shrink-0 rounded-full bg-ink-3" />
                    <span>{feature}</span>
                  </li>
                ))}
              </ul>
            </Panel>
          )}

          <Panel title="Опис">
            <p className="text-sm leading-relaxed whitespace-pre-line text-ink-2">
              {listing.description}
            </p>
          </Panel>

          <Questions listingId={listing.id} isSeller={auth.user?.id === listing.seller.id} />
        </div>

        <aside className="grid gap-4 lg:sticky lg:top-[74px]">
          <div className="card grid gap-3 p-4">
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <h1 className="font-display text-xl font-bold">
                  {car.make} {car.model}
                </h1>
                <p className="text-[13px] text-ink-2">
                  {car.generation ? `${car.generation} · ` : ''}
                  {car.year}
                </p>
              </div>

              <FavoriteButton
                listingId={listing.id}
                isFavorite={listing.isFavorite}
                size="large"
              />
            </div>

            {/* У лота з торгами ціну показує панель торгів — вона жива. */}
            {!isAuction && (
              <div>
                <div className="font-display text-[30px] leading-tight font-bold tabular-nums">
                  {formatPrice(listing.price, listing.currency)}
                </div>
                {listing.currency !== 'Uah' && (
                  <div className="font-mono text-[13px] text-ink-2 tabular-nums">
                    {formatPrice(listing.priceUah, 'Uah')}
                  </div>
                )}
              </div>
            )}

            {(listing.isNegotiable || listing.acceptsTrade || listing.isUrgent) && (
              <div className="flex flex-wrap gap-1.5">
                {listing.isNegotiable && <span className="pill pill-accent">Торг доречний</span>}
                {listing.acceptsTrade && <span className="pill pill-accent">Розглядаю обмін</span>}
                {listing.isUrgent && <span className="pill pill-accent">Терміновий продаж</span>}
              </div>
            )}

            {!isAuction && (
              <button type="button" className="btn btn-primary w-full py-3 text-base">
                Показати телефон
              </button>
            )}
          </div>

          {isAuction && <AuctionPanel listingId={listing.id} />}

          <div className="card grid gap-3 p-4">
            <span className="eyebrow">Продавець</span>

            <div>
              <div className="text-[15px] font-semibold">{listing.seller.displayName}</div>
              <div className="text-[13px] text-ink-2">
                {listing.seller.accountType === 'Dealer' ? 'Автосалон' : 'Приватна особа'}
              </div>
            </div>

            {listing.location && (
              <div className="border-t border-line pt-3 text-[13px] text-ink-2">
                <div className="text-ink">
                  {listing.location.cityName}
                  {listing.location.cityDistrictName
                    ? `, ${listing.location.cityDistrictName}`
                    : ''}
                </div>
                {listing.location.regionName}
              </div>
            )}
          </div>

          <div className="px-1 text-[13px] text-ink-3">
            {formatCount(listing.viewCount)}{' '}
            {plural(listing.viewCount, 'перегляд', 'перегляди', 'переглядів')}
          </div>
        </aside>
      </div>
    </div>
  )
}

/**
 * Сітка з волосяними лініями: тло контейнера — колір лінії, клітинки — колір
 * поверхні, а проміжок між ними рівно 1 px. Тло просвічує крізь проміжки й
 * дає ідеально тонкі роздільники без жодної рамки.
 */
function KeySpecs({ items }: { items: { label: string; value: string }[] }) {
  return (
    <dl className="grid grid-cols-2 gap-px overflow-hidden rounded-control border border-line bg-line sm:grid-cols-3">
      {items.map((item) => (
        <div key={item.label} className="grid gap-0.5 bg-surface px-3 py-2.5">
          <dt className="text-[11.5px] text-ink-3">{item.label}</dt>
          <dd className="text-[14.5px] font-semibold">{item.value}</dd>
        </div>
      ))}
    </dl>
  )
}

function Panel({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="card p-4">
      <h2 className="eyebrow mb-3">{title}</h2>
      {children}
    </section>
  )
}

/** Порожні характеристики не показуємо взагалі — рядок «—» лише засмічує таблицю. */
function Row({ label, value, mono }: { label: string; value: string | null; mono?: boolean }) {
  if (!value) return null

  return (
    <div className="flex items-baseline justify-between gap-4 border-b border-line py-2 last:border-0">
      <dt className="text-[13px] text-ink-2">{label}</dt>
      <dd className={`text-sm font-medium ${mono ? 'font-mono tabular-nums' : ''}`}>{value}</dd>
    </div>
  )
}

function Fact({ ok, text }: { ok: boolean; text: string }) {
  return <span className={`pill ${ok ? 'pill-good' : 'pill-danger'}`}>{text}</span>
}

function Notice({ children }: { children: React.ReactNode }) {
  return (
    <div className="wrap py-16">
      <p className="card p-10 text-center text-sm text-ink-2">{children}</p>
    </div>
  )
}

