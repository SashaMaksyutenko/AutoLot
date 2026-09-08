import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { fetchListing, type ListingDetails, type SellerSummary } from '../api/listing'
import { startConversation } from '../api/chat'
import { ApiError } from '../api/client'
import { useAttributeLabels } from '../api/useAttributeLabels'
import { useAuth } from '../auth/useAuth'
import { openSignIn } from '../auth/signInPrompt'
import { FavoriteButton } from '../components/FavoriteButton'
import { VerifiedMark } from '../components/catalog/ListingCard'
import { AuctionPanel } from '../components/listing/AuctionPanel'
import { Gallery } from '../components/listing/Gallery'
import { DealReviews } from '../components/listing/DealReviews'
import { PriceInsight } from '../components/listing/PriceInsight'
import { Questions } from '../components/listing/Questions'
import { RatingLine } from '../components/listing/Stars'
import { ReportButton } from '../components/listing/ReportButton'
import { formatCount, formatMileage, formatPrice } from '../format'
import { useTranslation } from '../i18n/useTranslation'

export function ListingPage() {
  const { t } = useTranslation()

  const { id } = useParams()
  const listingId = Number(id)

  const listing = useQuery({
    queryKey: ['listing', listingId],
    queryFn: ({ signal }) => fetchListing(listingId, signal),
    enabled: Number.isInteger(listingId) && listingId > 0,
  })

  if (listing.isPending) {
    return <Notice>{t('listing.loading')}</Notice>
  }

  if (listing.isError || !listing.data) {
    return (
      <Notice>
        {t('listing.notFound')}{' '}
        <Link to="/" className="text-accent hover:underline">
          {t('listing.backToCatalog')}
        </Link>
      </Notice>
    )
  }

  return <Loaded listing={listing.data} />
}

function Loaded({ listing }: { listing: ListingDetails }) {
  const { t, tPlural } = useTranslation()

  const auth = useAuth()
  const labelOf = useAttributeLabels()
  const car = listing.car
  const isAuction = listing.type === 'Auction'

  return (
    <div className="wrap grid gap-4 py-[26px]">
      <nav className="text-[13px] text-ink-2">
        <Link to="/" className="hover:text-accent hover:underline">
          {t('listing.catalogCrumb')}
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
              { label: t('spec.year'), value: String(car.year) },
              { label: t('spec.mileage'), value: formatMileage(car.mileage) },
              { label: t('spec.body'), value: labelOf('bodyTypes', car.bodyType) },
              { label: t('spec.fuel'), value: labelOf('fuelTypes', car.fuelType) },
              { label: t('spec.transmission'), value: labelOf('transmissions', car.transmission) },
              { label: t('spec.drivetrain'), value: labelOf('driveTypes', car.drivetrain) },
            ]}
          />

          <Panel title={t('listing.techPanel')}>
            <dl className="grid gap-x-10 sm:grid-cols-2">
              {/* Стан не входить у довідник перелічень — значень лише два. */}
              <Row label={t('spec.condition')} value={car.condition === 'New' ? t('spec.new') : t('spec.used')} />
              <Row
                label={t('spec.colour')}
                value={`${labelOf('colors', car.color)}${car.isMetallic ? t('spec.metallic') : ''}`}
              />
              <Row
                label={t('spec.engine')}
                value={car.engineVolume ? t('spec.litres', { value: car.engineVolume }) : null}
                mono
              />
              <Row
                label={t('spec.power')}
                value={car.enginePower ? t('spec.horsepower', { value: car.enginePower }) : null}
                mono
              />
              <Row
                label={t('spec.consumption')}
                value={
                  car.fuelConsumptionCombined
                    ? t('spec.consumptionValue', { value: car.fuelConsumptionCombined })
                    : null
                }
                mono
              />
              <Row
                label={t('spec.battery')}
                value={
                  car.batteryCapacity ? t('spec.batteryValue', { value: car.batteryCapacity }) : null
                }
                mono
              />
              <Row
                label={t('spec.range')}
                value={car.electricRange ? t('spec.rangeValue', { value: car.electricRange }) : null}
                mono
              />
              <Row label={t('spec.seats')} value={car.seatCount ? String(car.seatCount) : null} mono />
              <Row label={t('spec.doors')} value={car.doorCount ? String(car.doorCount) : null} mono />
              <Row label={t('spec.ecology')} value={car.ecologyStandard} />
              <Row label={t('spec.owners')} value={car.ownerCount ? String(car.ownerCount) : null} mono />
              <Row label="VIN" value={car.vin} mono />
            </dl>
          </Panel>

          <Panel title={t('listing.historyPanel')}>
            <div className="flex flex-wrap gap-1.5">
              <Fact
                ok={!car.wasInAccident}
                text={car.wasInAccident ? t('fact.accident') : t('fact.noAccident')}
              />
              <Fact
                ok={car.isCustomsCleared}
                text={car.isCustomsCleared ? t('fact.customsCleared') : t('fact.notCustomsCleared')}
              />
              <Fact
                ok={car.isLocatedInUkraine}
                text={car.isLocatedInUkraine ? t('fact.inUkraine') : t('fact.toOrder')}
              />
              {car.hasServiceBook && <Fact ok text={t('fact.serviceBook')} />}
              {car.isGarageKept && <Fact ok text={t('fact.garageKept')} />}
              {car.isOnCredit && <Fact ok={false} text={t('fact.onCredit')} />}
              {car.importedFromCountry && (
                <Fact ok text={t('fact.importedFrom', { country: car.importedFromCountry })} />
              )}
            </div>
          </Panel>

          {car.features.length > 0 && (
            <Panel title={t('listing.featuresPanel', { count: car.features.length })}>
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

          <Panel title={t('listing.descriptionPanel')}>
            <p className="text-sm leading-relaxed whitespace-pre-line text-ink-2">
              {listing.description}
            </p>
          </Panel>

          {/* Відгуки вище за питання: під проданим лотом важить угода, а не «чи бита». */}
          <DealReviews listingId={listing.id} />

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

            {/* Ринкова довідка одразу під ціною — саме там її шукають. */}
            {!isAuction && <PriceInsight listingId={listing.id} />}

            {(listing.isNegotiable || listing.acceptsTrade || listing.isUrgent) && (
              <div className="flex flex-wrap gap-1.5">
                {listing.isNegotiable && (
                  <span className="pill pill-accent">{t('listing.negotiable')}</span>
                )}
                {listing.acceptsTrade && (
                  <span className="pill pill-accent">{t('listing.acceptsTrade')}</span>
                )}
                {listing.isUrgent && (
                  <span className="pill pill-accent">{t('listing.urgent')}</span>
                )}
              </div>
            )}

            {!isAuction && <PhoneButton seller={listing.seller} />}

            {/* Продавцю власного лота писати нема кому. */}
            {auth.user?.id !== listing.seller.id && <WriteButton listingId={listing.id} />}
          </div>

          {isAuction && <AuctionPanel listingId={listing.id} />}

          <div className="card grid gap-3 p-4">
            <span className="eyebrow">{t('listing.seller')}</span>

            {/*
              Коли продає салон, показуємо салон із посиланням на вітрину, а не
              менеджера: покупцеві важливо, з ким він має справу, а не хто саме
              зі співробітників заповнював форму.
            */}
            {listing.dealer ? (
              <div>
                <Link
                  to={`/dealers/${listing.dealer.slug}`}
                  className="flex items-center gap-1.5 text-[15px] font-semibold hover:text-accent"
                >
                  {listing.dealer.isVerified && <VerifiedMark size={15} />}
                  {listing.dealer.name}
                </Link>
                <div className="text-[13px] text-ink-2">
                  {listing.dealer.isVerified ? t('listing.verifiedDealer') : t('listing.dealer')}
                </div>
              </div>
            ) : (
              <div>
                <div className="text-[15px] font-semibold">{listing.seller.displayName}</div>
                <div className="text-[13px] text-ink-2">{t('listing.privateSeller')}</div>
              </div>
            )}

            {/*
              Рейтинг веде на профіль. Саме він — вхід у репутацію: побачивши
              «4,7 (12)», покупець захоче прочитати ті дванадцять відгуків, і
              йому має бути куди натиснути.
            */}
            <Link
              to={`/users/${listing.seller.id}`}
              className="justify-self-start hover:text-accent"
            >
              <RatingLine
                count={listing.seller.rating.count}
                average={listing.seller.rating.average}
              />
            </Link>

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

            {/* На власний лот скаржитися нема сенсу — сервер таку скаргу й не прийме. */}
            {auth.user?.id !== listing.seller.id && <ReportButton listingId={listing.id} />}
          </div>

          <div className="px-1 text-[13px] text-ink-3">
            {tPlural('listing.views', listing.viewCount, {
              count: formatCount(listing.viewCount),
            })}
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
/**
 * Кнопка «Показати телефон». Досі вона нічого не робила — номер не доходив
 * до сторінки.
 *
 * Гостю номер не показуємо, і не приховуванням на клієнті: сервер його
 * взагалі не віддає. Відкритий номер у публічній відповіді збирають роботи
 * за години, і продавець потім роками отримує дзвінки посередників.
 *
 * Номер відкривається натисканням, а не одразу: так його не видно тим, хто
 * просто гортає сторінки, і збирати номери стає дорожче.
 */
function PhoneButton({ seller }: { seller: SellerSummary }) {
  const { t } = useTranslation()

  const [shown, setShown] = useState(false)
  const auth = useAuth()

  if (!auth.user) {
    return (
      <button
        type="button"
        onClick={openSignIn}
        className="btn btn-primary w-full py-3 text-base"
      >
        {t('phone.signIn')}
      </button>
    )
  }

  if (seller.phoneNumber === null) {
    return (
      <p className="rounded-control bg-surface-2 px-3 py-2 text-center text-[13px] text-ink-2">
        {t('phone.none')}
      </p>
    )
  }

  if (!shown) {
    return (
      <button
        type="button"
        onClick={() => setShown(true)}
        className="btn btn-primary w-full py-3 text-base"
      >
        {t('phone.show')}
      </button>
    )
  }

  return (
    <a
      href={`tel:${seller.phoneNumber}`}
      className="btn btn-primary w-full py-3 font-mono text-base tracking-wide"
    >
      {seller.phoneNumber}
    </a>
  )
}

/**
 * «Написати продавцю» — приватне листування, на відміну від питань під лотом.
 *
 * Розмову починає сервер: він знаходить наявну гілку про це оголошення або
 * створює нову. Тому натискати можна скільки завгодно — другої гілки про те
 * саме авто не з'явиться.
 */
function WriteButton({ listingId }: { listingId: number }) {
  const { t } = useTranslation()

  const auth = useAuth()
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)

  const start = useMutation({
    mutationFn: () => startConversation(listingId),
    onSuccess: (conversation) => navigate(`/chat?id=${conversation.id}`),
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : t('chat.startFailed')),
  })

  if (!auth.user) {
    return (
      <button type="button" onClick={openSignIn} className="btn w-full py-3">
        {t('chat.signInToWrite')}
      </button>
    )
  }

  return (
    <>
      <button
        type="button"
        onClick={() => start.mutate()}
        disabled={start.isPending}
        className="btn w-full py-3"
      >
        {start.isPending ? t('chat.opening') : t('chat.writeToSeller')}
      </button>

      {error && <p className="text-[12px] text-danger">{error}</p>}
    </>
  )
}

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

