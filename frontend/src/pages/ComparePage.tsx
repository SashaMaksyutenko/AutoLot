import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { fetchListingsForCompare, type ListingDetails } from '../api/listing'
import { useAttributeLabels } from '../api/useAttributeLabels'
import { useCompare } from '../compare/useCompare'
import { formatMileage, formatPrice, plural } from '../format'

/**
 * Порівняння авто пліч-о-пліч.
 *
 * Уся користь таблиці — у відмінностях. Тридцять рядків, з яких двадцять сім
 * однакові, читати неможливо, тому рядки, де всі значення збігаються,
 * приглушені, а перемикач лишає самі відмінності.
 */
export function ComparePage() {
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
        Ви ще нічого не відклали для порівняння. Відкрийте{' '}
        <Link to="/" className="text-accent hover:underline">
          каталог
        </Link>{' '}
        і натисніть «Порівняти» на картці авто.
      </Notice>
    )
  }

  if (listings.isPending) {
    return <Notice>Завантажуємо…</Notice>
  }

  if (listings.isError || !listings.data) {
    return <Notice>Не вдалося отримати оголошення.</Notice>
  }

  const cars = listings.data

  if (cars.length === 0) {
    return (
      <Notice>
        Жодне з відкладених авто вже не продається.{' '}
        <button type="button" onClick={compare.clear} className="text-accent hover:underline">
          Очистити список
        </button>
      </Notice>
    )
  }

  const rows = buildRows(cars, labelOf)
  const differing = rows.filter((row) => row.differs).length

  return (
    <div className="wrap grid gap-4 py-[26px]">
      <div className="flex flex-wrap items-baseline justify-between gap-3">
        <div>
          <h1 className="font-display text-[25px] font-bold">Порівняння</h1>
          <p className="text-[13px] text-ink-2">
            {cars.length} {plural(cars.length, 'авто', 'авто', 'авто')} ·{' '}
            {differing} {plural(differing, 'відмінність', 'відмінності', 'відмінностей')}
            {cars.length < compare.ids.length && ' · частина вже не продається'}
          </p>
        </div>

        <button type="button" onClick={compare.clear} className="btn">
          Очистити
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
                <span className="eyebrow">Характеристика</span>
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
          без фото
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
        Прибрати
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
): Row[] {
  const definitions: { label: string; of: (listing: ListingDetails) => string | null }[] = [
    { label: 'Рік випуску', of: (l) => String(l.car.year) },
    { label: 'Пробіг', of: (l) => formatMileage(l.car.mileage) },
    { label: 'Стан', of: (l) => (l.car.condition === 'New' ? 'Новий' : 'Вживаний') },
    { label: 'Кузов', of: (l) => labelOf('bodyTypes', l.car.bodyType) },
    { label: 'Пальне', of: (l) => labelOf('fuelTypes', l.car.fuelType) },
    { label: 'Коробка', of: (l) => labelOf('transmissions', l.car.transmission) },
    { label: 'Привід', of: (l) => labelOf('driveTypes', l.car.drivetrain) },
    { label: 'Колір', of: (l) => labelOf('colors', l.car.color) },
    { label: "Об'єм двигуна", of: (l) => (l.car.engineVolume ? `${l.car.engineVolume} л` : null) },
    { label: 'Потужність', of: (l) => (l.car.enginePower ? `${l.car.enginePower} к.с.` : null) },
    {
      label: 'Витрата, змішана',
      of: (l) => (l.car.fuelConsumptionCombined ? `${l.car.fuelConsumptionCombined} л/100 км` : null),
    },
    {
      label: 'Батарея',
      of: (l) => (l.car.batteryCapacity ? `${l.car.batteryCapacity} кВт·год` : null),
    },
    { label: 'Запас ходу', of: (l) => (l.car.electricRange ? `${l.car.electricRange} км` : null) },
    { label: 'Місць', of: (l) => (l.car.seatCount ? String(l.car.seatCount) : null) },
    { label: 'Дверей', of: (l) => (l.car.doorCount ? String(l.car.doorCount) : null) },
    { label: 'Власників', of: (l) => (l.car.ownerCount ? String(l.car.ownerCount) : null) },
    { label: 'Екостандарт', of: (l) => l.car.ecologyStandard },
    { label: 'Був у ДТП', of: (l) => (l.car.wasInAccident ? 'Так' : 'Ні') },
    { label: 'Розмитнений', of: (l) => (l.car.isCustomsCleared ? 'Так' : 'Ні') },
    { label: 'В Україні', of: (l) => (l.car.isLocatedInUkraine ? 'Так' : 'Ні') },
    { label: 'Сервісна книжка', of: (l) => (l.car.hasServiceBook ? 'Є' : 'Немає') },
    { label: 'Пригнаний з', of: (l) => l.car.importedFromCountry },
    { label: 'Місто', of: (l) => l.location?.cityName ?? null },
    {
      label: 'Опцій у комплектації',
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
