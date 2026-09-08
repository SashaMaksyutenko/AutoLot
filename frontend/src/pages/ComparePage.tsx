import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { fetchListingsForCompare, type ListingDetails } from '../api/listing'
import { useAttributeLabels } from '../api/useAttributeLabels'
import { useCompare } from '../compare/useCompare'
import { formatMileage, formatPrice } from '../format'
import { useTranslation } from '../i18n/useTranslation'
import type { MessageKey } from '../i18n/messages'

/**
 * Порівняння авто пліч-о-пліч.
 *
 * Уся користь таблиці — у відмінностях. Тридцять рядків, з яких двадцять сім
 * однакові, читати неможливо, тому рядки, де всі значення збігаються,
 * приглушені, а перемикач лишає самі відмінності.
 */
export function ComparePage() {
  const { t, tPlural } = useTranslation()

  const compare = useCompare()
  const labelOf = useAttributeLabels()

  const listings = useQuery({
    queryKey: ['compare', compare.ids],
    queryFn: ({ signal }) => fetchListingsForCompare(compare.ids, signal),
    enabled: compare.ids.length > 0,
  })

  if (compare.ids.length === 0) {
    return (
      <Notice>
        {t('compare.emptyLead')}{' '}
        <Link to="/" className="text-accent hover:underline">
          {t('compare.catalog')}
        </Link>{' '}
        {t('compare.emptyTail')}
      </Notice>
    )
  }

  if (listings.isPending) {
    return <Notice>{t('compare.loading')}</Notice>
  }

  if (listings.isError || !listings.data) {
    return <Notice>{t('compare.failed')}</Notice>
  }

  const cars = listings.data

  if (cars.length === 0) {
    return (
      <Notice>
        {t('compare.allGone')}{' '}
        <button type="button" onClick={compare.clear} className="text-accent hover:underline">
          {t('compare.clearList')}
        </button>
      </Notice>
    )
  }

  const rows = buildRows(cars, labelOf, t)
  const differing = rows.filter((row) => row.differs).length

  return (
    <div className="wrap grid gap-4 py-[26px]">
      <div className="flex flex-wrap items-baseline justify-between gap-3">
        <div>
          <h1 className="font-display text-[25px] font-bold">{t('compare.title')}</h1>
          <p className="text-[13px] text-ink-2">
            {tPlural('compare.cars', cars.length)} ·{' '}
            {tPlural('compare.differences', differing)}
            {cars.length < compare.ids.length && t('compare.somePulled')}
          </p>
        </div>

        <button type="button" onClick={compare.clear} className="btn">
          {t('compare.clear')}
        </button>
      </div>

      {/*
        Таблиця ширша за екран на телефоні — і це нормально, якщо прокрутка
        живе всередині власного контейнера. Прокручувати всю сторінку вбік
        значно гірше: тоді «їде» й шапка, і решта.
      */}
      <div className="card overflow-x-auto">
        <table className="w-full border-collapse text-[13px]">
          <thead>
            <tr>
              <th className="sticky left-0 z-10 bg-surface p-3 text-left align-bottom">
                <span className="eyebrow">{t('compare.characteristic')}</span>
              </th>

              {cars.map((car) => (
                <th key={car.id} className="min-w-[190px] p-3 text-left align-bottom">
                  <CarHeader listing={car} onRemove={() => compare.toggle(car.id)} />
                </th>
              ))}
            </tr>
          </thead>

          <tbody>
            {rows.map((row) => (
              <tr
                key={row.label}
                className={`border-t border-line ${row.differs ? '' : 'text-ink-3'}`}
              >
                {/*
                  Перша колонка не їде вбік разом із рештою: без неї значення
                  посеред прокрутки перестають щось означати.
                */}
                <th className="sticky left-0 z-10 bg-surface p-3 text-left font-normal text-ink-2">
                  {row.label}
                </th>

                {row.values.map((value, index) => (
                  <td
                    key={cars[index].id}
                    className={`p-3 ${row.differs ? 'font-medium text-ink' : ''}`}
                  >
                    {value ?? <span className="text-ink-3">—</span>}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

function CarHeader({ listing, onRemove }: { listing: ListingDetails; onRemove: () => void }) {
  const { t } = useTranslation()

  return (
    <div className="grid gap-1">
      {listing.photos.length > 0 ? (
        <img
          src={`/media/${listing.photos[0].path}`}
          alt=""
          className="h-[80px] w-full rounded-control border border-line object-cover"
        />
      ) : (
        <span className="grid h-[80px] place-items-center rounded-control border border-line bg-surface-2 text-[11px] text-ink-3">
          {t('compare.noPhoto')}
        </span>
      )}

      <Link
        to={`/listing/${listing.id}`}
        className="font-display text-[14.5px] font-semibold hover:text-accent"
      >
        {listing.car.make} {listing.car.model}
      </Link>

      <span className="font-mono text-[14px] tabular-nums">
        {formatPrice(listing.price, listing.currency)}
      </span>

      <button
        type="button"
        onClick={onRemove}
        className="justify-self-start text-[12px] text-ink-3 hover:text-danger"
      >
        {t('compare.removeCar')}
      </button>
    </div>
  )
}

interface Row {
  label: string
  values: (string | null)[]

  /** Чи є між авто різниця. Однакові рядки приглушуємо. */
  differs: boolean
}

/**
 * Збирає рядки таблиці.
 *
 * Порівнюємо саме РЯДКИ показу, а не сирі поля: людині важливо, що в одного
 * «Механіка», а в другого «Автомат», і байдуже, що під ними різні числа
 * перелічення.
 */
function buildRows(
  cars: ListingDetails[],
  labelOf: ReturnType<typeof useAttributeLabels>,
  t: (key: MessageKey, values?: Record<string, string | number>) => string,
): Row[] {
  const definitions: { label: string; of: (listing: ListingDetails) => string | null }[] = [
    { label: t('spec.year'), of: (l) => String(l.car.year) },
    { label: t('spec.mileage'), of: (l) => formatMileage(l.car.mileage) },
    {
      label: t('spec.condition'),
      of: (l) => (l.car.condition === 'New' ? t('spec.new') : t('spec.used')),
    },
    { label: t('spec.body'), of: (l) => labelOf('bodyTypes', l.car.bodyType) },
    { label: t('spec.fuel'), of: (l) => labelOf('fuelTypes', l.car.fuelType) },
    { label: t('spec.transmission'), of: (l) => labelOf('transmissions', l.car.transmission) },
    { label: t('spec.drivetrain'), of: (l) => labelOf('driveTypes', l.car.drivetrain) },
    { label: t('spec.colour'), of: (l) => labelOf('colors', l.car.color) },
    {
      label: t('row.engineVolume'),
      of: (l) => (l.car.engineVolume ? t('spec.litres', { value: l.car.engineVolume }) : null),
    },
    {
      label: t('spec.power'),
      of: (l) => (l.car.enginePower ? t('spec.horsepower', { value: l.car.enginePower }) : null),
    },
    {
      label: t('spec.consumption'),
      of: (l) =>
        l.car.fuelConsumptionCombined
          ? t('spec.consumptionValue', { value: l.car.fuelConsumptionCombined })
          : null,
    },
    {
      label: t('spec.battery'),
      of: (l) =>
        l.car.batteryCapacity ? t('spec.batteryValue', { value: l.car.batteryCapacity }) : null,
    },
    {
      label: t('spec.range'),
      of: (l) => (l.car.electricRange ? t('spec.rangeValue', { value: l.car.electricRange }) : null),
    },
    { label: t('spec.seats'), of: (l) => (l.car.seatCount ? String(l.car.seatCount) : null) },
    { label: t('spec.doors'), of: (l) => (l.car.doorCount ? String(l.car.doorCount) : null) },
    {
      label: t('spec.owners'),
      of: (l) => (l.car.ownerCount ? String(l.car.ownerCount) : null),
    },
    { label: t('spec.ecology'), of: (l) => l.car.ecologyStandard },
    {
      label: t('row.wasInAccident'),
      of: (l) => (l.car.wasInAccident ? t('row.yes') : t('row.no')),
    },
    {
      label: t('row.customsCleared'),
      of: (l) => (l.car.isCustomsCleared ? t('row.yes') : t('row.no')),
    },
    {
      label: t('row.inUkraine'),
      of: (l) => (l.car.isLocatedInUkraine ? t('row.yes') : t('row.no')),
    },
    {
      label: t('row.serviceBook'),
      of: (l) => (l.car.hasServiceBook ? t('row.has') : t('row.hasNot')),
    },
    { label: t('row.importedFrom'), of: (l) => l.car.importedFromCountry },
    { label: t('row.city'), of: (l) => l.location?.cityName ?? null },
    {
      label: t('row.featureCount'),
      of: (l) => (l.car.features.length > 0 ? String(l.car.features.length) : null),
    },
  ]

  return definitions.map((definition) => {
    const values = cars.map((car) => definition.of(car))

    return {
      label: definition.label,
      values,
      differs: new Set(values.map((value) => value ?? '')).size > 1,
    }
  })
}

function Notice({ children }: { children: React.ReactNode }) {
  return (
    <div className="wrap py-16">
      <p className="card mx-auto max-w-[520px] p-10 text-center text-sm text-ink-2">{children}</p>
    </div>
  )
}
