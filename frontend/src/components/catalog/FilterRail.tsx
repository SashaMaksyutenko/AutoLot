import { useQuery } from '@tanstack/react-query'
import type { CatalogFilters, ListingType } from '../../api/catalog'
import {
  fetchCarAttributes,
  fetchCities,
  fetchMakes,
  fetchModels,
  fetchRegions,
  type LookupItem,
} from '../../api/reference'

interface Props {
  filters: CatalogFilters
  onChange: (patch: Partial<CatalogFilters>) => void
  onReset: () => void
  totalCount: number
}

/**
 * Панель фільтрів. Довідники тягнуться з бекенду, тож перелік кузовів чи міст
 * тут не зашитий — додати новий тип пального можна без зміни фронтенду.
 */
export function FilterRail({ filters, onChange, onReset, totalCount }: Props) {
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
    <aside className="flex w-[268px] shrink-0 flex-col gap-px self-start overflow-hidden rounded-md border border-line bg-line">
      <Section>
        <div className="flex items-center justify-between">
          <span className="text-xs font-semibold tracking-wider text-muted uppercase">
            Фільтри
          </span>
          <button type="button" onClick={onReset} className="text-xs text-brand hover:underline">
            Очистити
          </button>
        </div>
      </Section>

      <Section title="Тип продажу">
        <div className="flex gap-1.5">
          <SaleType label="Усі" active={filters.type === undefined} onClick={() => onChange({ type: undefined })} />
          <SaleType label="Ціна" active={filters.type === 'FixedPrice'} onClick={() => onChange({ type: 'FixedPrice' })} />
          <SaleType label="Торги" active={filters.type === 'Auction'} onClick={() => onChange({ type: 'Auction' })} lot />
        </div>
      </Section>

      <Section title="Марка і модель">
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
      </Section>

      <Section title="Ціна">
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
          <div className="flex shrink-0 gap-px rounded-sm bg-[#e4e8ec] p-0.5 font-mono text-[11px]">
            {(['Usd', 'Eur', 'Uah'] as const).map((currency) => (
              <button
                key={currency}
                type="button"
                onClick={() => onChange({ priceCurrency: currency })}
                className={`rounded-[3px] px-1.5 py-0.5 ${
                  filters.priceCurrency === currency
                    ? 'bg-surface font-semibold text-ink'
                    : 'text-subtle'
                }`}
              >
                {currency === 'Usd' ? '$' : currency === 'Eur' ? '€' : '₴'}
              </button>
            ))}
          </div>
        </div>
      </Section>

      <Section title="Рік випуску">
        <div className="flex gap-2">
          <NumberInput value={filters.yearFrom} placeholder="від" onChange={(yearFrom) => onChange({ yearFrom })} />
          <NumberInput value={filters.yearTo} placeholder="до" onChange={(yearTo) => onChange({ yearTo })} />
        </div>
      </Section>

      <Section title="Пробіг, км">
        <NumberInput value={filters.mileageTo} placeholder="до" onChange={(mileageTo) => onChange({ mileageTo })} />
      </Section>

      <Section title="Тип пального">
        <Chips
          options={attributes.data?.fuelTypes ?? []}
          selected={filters.fuelTypes}
          onToggle={(fuelTypes) => onChange({ fuelTypes })}
        />
      </Section>

      <Section title="Кузов">
        <Chips
          options={attributes.data?.bodyTypes ?? []}
          selected={filters.bodyTypes}
          onToggle={(bodyTypes) => onChange({ bodyTypes })}
        />
      </Section>

      <Section title="Коробка передач">
        <Chips
          options={attributes.data?.transmissions ?? []}
          selected={filters.transmissions}
          onToggle={(transmissions) => onChange({ transmissions })}
        />
      </Section>

      <Section title="Регіон">
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
      </Section>

      <Section>
        <label className="flex cursor-pointer items-center gap-2.5 text-sm">
          <input
            type="checkbox"
            checked={filters.wasInAccident === false}
            onChange={(event) =>
              onChange({ wasInAccident: event.target.checked ? false : undefined })
            }
            className="h-4 w-4 accent-brand"
          />
          <span>Не був у ДТП</span>
        </label>
      </Section>

      <Section>
        <div className="flex h-10 items-center justify-center rounded-sm bg-brand text-sm font-semibold text-white">
          Знайдено {totalCount}
        </div>
      </Section>
    </aside>
  )
}

function Section({ title, children }: { title?: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-2.5 bg-surface p-4">
      {title && <div className="text-xs font-semibold text-muted">{title}</div>}
      {children}
    </div>
  )
}

function SaleType({
  label,
  active,
  onClick,
  lot,
}: {
  label: string
  active: boolean
  onClick: () => void
  lot?: boolean
}) {
  const activeStyle = lot ? 'border-lot-line bg-lot-soft text-lot-ink' : 'bg-brand text-white'

  return (
    <button
      type="button"
      onClick={onClick}
      className={`flex-grow rounded-sm border py-1.5 text-[13px] ${
        active ? `font-semibold ${activeStyle}` : 'border-line-strong text-muted'
      } ${active && !lot ? 'border-brand' : ''}`}
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
      className="h-9 rounded-sm border border-line-strong bg-surface px-2.5 text-sm disabled:bg-[#f6f8f9] disabled:text-faint"
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
      className="h-9 w-full min-w-0 rounded-sm border border-line-strong bg-surface px-2.5 font-mono text-sm placeholder:font-sans placeholder:text-faint"
    />
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
            onClick={() =>
              onToggle(
                active
                  ? selected.filter((value) => value !== option.value)
                  : [...selected, option.value],
              )
            }
            className={`rounded-sm border px-2.5 py-1 text-[13px] ${
              active
                ? 'border-brand bg-brand-soft font-medium text-brand'
                : 'border-line-strong text-muted'
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
