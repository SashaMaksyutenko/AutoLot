import { useQuery } from '@tanstack/react-query'
import type { CatalogFilters, Currency, ListingType } from '../../api/catalog'
import {
  fetchCarAttributes,
  fetchCities,
  fetchMakes,
  fetchModels,
  fetchRegions,
  type LookupItem,
} from '../../api/reference'
import { SavedSearches } from './SavedSearches'

interface Props {
  filters: CatalogFilters
  onChange: (patch: Partial<CatalogFilters>) => void

  /**
   * Замінити фільтри цілком. Окремо від onChange: збережений пошук не
   * доповнює поточні умови, а стає ними — інакше залишки попереднього
   * фільтра тихо звужували б відновлений пошук.
   */
  onApply: (filters: CatalogFilters) => void
  onReset: () => void
  totalCount: number
}

/**
 * Панель фільтрів. Довідники тягнуться з бекенду, тож перелік кузовів чи міст
 * тут не зашитий — додати новий тип пального можна без зміни фронтенду.
 */
export function FilterRail({ filters, onChange, onApply, onReset, totalCount }: Props) {
  const attributes = useQuery({
    queryKey: ['car-attributes'],
    queryFn: ({ signal }) => fetchCarAttributes(signal),
    staleTime: Infinity,
  })

  const makes = useQuery({
    queryKey: ['makes'],
    queryFn: ({ signal }) => fetchMakes(signal),
    staleTime: Infinity,
  })

  const models = useQuery({
    queryKey: ['models', filters.makeId],
    queryFn: ({ signal }) => fetchModels(filters.makeId!, signal),
    enabled: filters.makeId !== undefined,
    staleTime: Infinity,
  })

  const regions = useQuery({
    queryKey: ['regions'],
    queryFn: ({ signal }) => fetchRegions(signal),
    staleTime: Infinity,
  })

  const cities = useQuery({
    queryKey: ['cities', filters.regionId],
    queryFn: ({ signal }) => fetchCities(filters.regionId!, signal),
    enabled: filters.regionId !== undefined,
    staleTime: Infinity,
  })

  return (
    // sticky вмикається лише з ширини lg: у вузькій одноколонковій розкладці
    // «прилипла» панель фільтрів заважала б прокручувати видачу.
    <aside className="card self-start px-4 pt-1 pb-4 lg:sticky lg:top-[74px]">
      <Group>
        <div className="flex items-center justify-between">
          <span className="eyebrow">Фільтри</span>
          <button
            type="button"
            onClick={onReset}
            className="text-xs text-accent hover:underline"
          >
            Очистити
          </button>
        </div>
      </Group>

      <Group>
        <SavedSearches filters={filters} onApply={onApply} />
      </Group>

      <Group title="Тип продажу">
        {/*
          Три кнопки, з яких активна завжди одна — це «радіо» без круглих
          позначок. aria-pressed усередині SaleType повідомляє програмам для
          незрячих, яка з них зараз натиснута.
        */}
        <div className="flex overflow-hidden rounded-control border border-line">
          <SaleType
            label="Усі"
            active={filters.type === undefined}
            onClick={() => onChange({ type: undefined })}
          />
          <SaleType
            label="Ціна"
            active={filters.type === 'FixedPrice'}
            onClick={() => onChange({ type: 'FixedPrice' })}
          />
          <SaleType
            label="Торги"
            active={filters.type === 'Auction'}
            onClick={() => onChange({ type: 'Auction' })}
          />
        </div>
      </Group>

      <Group title="Хто продає">
        {/*
          Тризначний вибір, а не прапорець: обидва боки однаково потрібні.
          Одні шукають гарантію салону, інші свідомо йдуть до приватника,
          щоб не переплачувати.
        */}
        <div className="flex overflow-hidden rounded-control border border-line">
          <SaleType
            label="Усі"
            active={filters.fromDealer === undefined}
            onClick={() => onChange({ fromDealer: undefined, verifiedDealerOnly: undefined })}
          />
          <SaleType
            label="Салони"
            active={filters.fromDealer === true}
            onClick={() => onChange({ fromDealer: true })}
          />
          <SaleType
            label="Приватні"
            active={filters.fromDealer === false}
            onClick={() => onChange({ fromDealer: false, verifiedDealerOnly: undefined })}
          />
        </div>

        {/* Уточнення має сенс лише коли обрано салони. */}
        {filters.fromDealer === true && (
          <label className="flex cursor-pointer items-center gap-2 text-[13.5px]">
            <input
              type="checkbox"
              checked={filters.verifiedDealerOnly === true}
              onChange={(event) =>
                onChange({ verifiedDealerOnly: event.target.checked ? true : undefined })
              }
              className="h-[15px] w-[15px] accent-accent"
            />
            <span>Лише перевірені</span>
          </label>
        )}
      </Group>

      <Group title="Марка і модель">
        <Select
          value={filters.makeId}
          placeholder="Будь-яка марка"
          options={(makes.data ?? []).map((make) => ({ id: make.id, name: make.name }))}
          // Модель належить марці, тож зміна марки скидає раніше обрану модель.
          onChange={(makeId) => onChange({ makeId, modelId: undefined })}
        />
        <Select
          value={filters.modelId}
          placeholder={filters.makeId ? 'Будь-яка модель' : 'Спершу оберіть марку'}
          disabled={filters.makeId === undefined}
          options={(models.data ?? []).map((model) => ({ id: model.id, name: model.name }))}
          onChange={(modelId) => onChange({ modelId })}
        />
      </Group>

      <Group title="Ціна">
        <div className="flex gap-2">
          <NumberInput
            value={filters.priceFrom}
            placeholder="від"
            onChange={(priceFrom) => onChange({ priceFrom })}
          />
          <NumberInput
            value={filters.priceTo}
            placeholder="до"
            onChange={(priceTo) => onChange({ priceTo })}
          />
          <CurrencyPicker
            value={filters.priceCurrency}
            onChange={(priceCurrency) => onChange({ priceCurrency })}
          />
        </div>
      </Group>

      <Group title="Рік випуску">
        <div className="flex gap-2">
          <NumberInput
            value={filters.yearFrom}
            placeholder="від"
            onChange={(yearFrom) => onChange({ yearFrom })}
          />
          <NumberInput
            value={filters.yearTo}
            placeholder="до"
            onChange={(yearTo) => onChange({ yearTo })}
          />
        </div>
      </Group>

      <Group title="Пробіг, км">
        <NumberInput
          value={filters.mileageTo}
          placeholder="до"
          onChange={(mileageTo) => onChange({ mileageTo })}
        />
      </Group>

      <Group title="Тип пального">
        <Chips
          options={attributes.data?.fuelTypes ?? []}
          selected={filters.fuelTypes}
          onToggle={(fuelTypes) => onChange({ fuelTypes })}
        />
      </Group>

      <Group title="Кузов">
        <Chips
          options={attributes.data?.bodyTypes ?? []}
          selected={filters.bodyTypes}
          onToggle={(bodyTypes) => onChange({ bodyTypes })}
        />
      </Group>

      <Group title="Коробка передач">
        <Chips
          options={attributes.data?.transmissions ?? []}
          selected={filters.transmissions}
          onToggle={(transmissions) => onChange({ transmissions })}
        />
      </Group>

      <Group title="Регіон">
        <Select
          value={filters.regionId}
          placeholder="Уся Україна"
          options={regions.data ?? []}
          onChange={(regionId) => onChange({ regionId, cityId: undefined })}
        />
        <Select
          value={filters.cityId}
          placeholder={filters.regionId ? 'Усі міста' : 'Спершу оберіть регіон'}
          disabled={filters.regionId === undefined}
          options={cities.data ?? []}
          onChange={(cityId) => onChange({ cityId })}
        />
      </Group>

      <Group>
        <label className="flex cursor-pointer items-center gap-2 text-[13.5px]">
          <input
            type="checkbox"
            checked={filters.wasInAccident === false}
            onChange={(event) =>
              onChange({ wasInAccident: event.target.checked ? false : undefined })
            }
            // accent-accent фарбує саму «галочку» бірюзовим замість
            // синього кольору браузера за замовчуванням.
            className="h-[15px] w-[15px] accent-accent"
          />
          <span>Не був у ДТП</span>
        </label>
      </Group>

      <Group>
        <div className="btn btn-primary w-full">Знайдено {totalCount}</div>
      </Group>
    </aside>
  )
}

/** Смуга фільтра з підписом. Останню знизу не підкреслюємо — там уже край картки. */
function Group({ title, children }: { title?: string; children: React.ReactNode }) {
  return (
    <div className="grid gap-2.5 border-b border-line py-3.5 last:border-0 last:pb-0">
      {title && (
        <h4 className="font-display text-xs font-bold tracking-[0.09em] text-ink-3 uppercase">
          {title}
        </h4>
      )}
      {children}
    </div>
  )
}

function SaleType({
  label,
  active,
  onClick,
}: {
  label: string
  active: boolean
  onClick: () => void
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      className={`flex-1 border-line px-1 py-1.5 text-[13px] not-first:border-l ${
        active ? 'bg-accent-soft font-semibold text-accent' : 'bg-surface text-ink-2'
      }`}
    >
      {label}
    </button>
  )
}

function Select({
  value,
  placeholder,
  options,
  onChange,
  disabled,
}: {
  value: number | undefined
  placeholder: string
  options: { id: number; name: string }[]
  onChange: (value: number | undefined) => void
  disabled?: boolean
}) {
  return (
    <select
      value={value ?? ''}
      disabled={disabled}
      onChange={(event) => onChange(event.target.value ? Number(event.target.value) : undefined)}
      className="control"
    >
      <option value="">{placeholder}</option>
      {options.map((option) => (
        <option key={option.id} value={option.id}>
          {option.name}
        </option>
      ))}
    </select>
  )
}

function NumberInput({
  value,
  placeholder,
  onChange,
}: {
  value: number | undefined
  placeholder: string
  onChange: (value: number | undefined) => void
}) {
  return (
    <input
      type="number"
      inputMode="numeric"
      value={value ?? ''}
      placeholder={placeholder}
      onChange={(event) => onChange(event.target.value ? Number(event.target.value) : undefined)}
      className="control min-w-0 font-mono tabular-nums placeholder:font-sans placeholder:text-ink-3"
    />
  )
}

const currencySigns: Record<Currency, string> = { Usd: '$', Eur: '€', Uah: '₴' }

/** У якій валюті введено «від» і «до». Бекенд сам переведе межі в гривню. */
function CurrencyPicker({
  value,
  onChange,
}: {
  value: Currency
  onChange: (value: Currency) => void
}) {
  return (
    <div className="flex shrink-0 gap-0.5 rounded-control border border-line bg-surface-2 p-0.5">
      {(Object.keys(currencySigns) as Currency[]).map((currency) => (
        <button
          key={currency}
          type="button"
          onClick={() => onChange(currency)}
          aria-pressed={value === currency}
          className={`rounded-[4px] px-1.5 font-mono text-xs ${
            value === currency ? 'bg-ink font-semibold text-surface' : 'text-ink-2'
          }`}
        >
          {currencySigns[currency]}
        </button>
      ))}
    </div>
  )
}

/** Набір значень: обране означає «або», тож можна тримати кілька одразу. */
function Chips({
  options,
  selected,
  onToggle,
}: {
  options: LookupItem[]
  selected: string[]
  onToggle: (values: string[]) => void
}) {
  return (
    <div className="flex flex-wrap gap-1.5">
      {options.map((option) => {
        const active = selected.includes(option.value)

        return (
          <button
            key={option.value}
            type="button"
            aria-pressed={active}
            onClick={() =>
              onToggle(
                active
                  ? selected.filter((value) => value !== option.value)
                  : [...selected, option.value],
              )
            }
            className={`rounded-control border px-2.5 py-1 text-[13px] ${
              active
                ? 'border-accent bg-accent-soft font-medium text-accent'
                : 'border-line text-ink-2 hover:border-ink-3'
            }`}
          >
            {option.name}
          </button>
        )
      })}
    </div>
  )
}

export type { ListingType }
