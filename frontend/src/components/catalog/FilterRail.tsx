import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import type { CatalogFilters, Currency, ListingType } from '../../api/catalog'
import {
  fetchCarAttributes,
  fetchCities,
  fetchCityDistricts,
  fetchCountries,
  fetchGenerations,
  fetchMakes,
  fetchModels,
  fetchRegions,
  type LookupItem,
} from '../../api/reference'
import { formatCount } from '../../format'
import { useTranslation } from '../../i18n/useTranslation'
import { FeaturePicker } from './FeaturePicker'
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
  const { t } = useTranslation()

  // Розширений блок згорнутий за замовчуванням — пояснення біля самої кнопки.
  const [advanced, setAdvanced] = useState(false)

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

  const generations = useQuery({
    queryKey: ['generations', filters.modelId],
    queryFn: ({ signal }) => fetchGenerations(filters.modelId!, signal),
    enabled: filters.modelId !== undefined,
    staleTime: Infinity,
  })

  const cityDistricts = useQuery({
    queryKey: ['city-districts', filters.cityId],
    queryFn: ({ signal }) => fetchCityDistricts(filters.cityId!, signal),
    enabled: filters.cityId !== undefined,
    staleTime: Infinity,
  })

  const countries = useQuery({
    queryKey: ['countries'],
    queryFn: ({ signal }) => fetchCountries(signal),
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
          <span className="eyebrow">{t('filter.title')}</span>
          <button
            type="button"
            onClick={onReset}
            className="text-xs text-accent hover:underline"
          >
            {t('filter.clear')}
          </button>
        </div>
      </Group>

      <Group>
        <input
          value={filters.text ?? ''}
          onChange={(event) => onChange({ text: event.target.value || undefined })}
          placeholder={t('filter.search')}
          className="control text-[13px]"
        />
      </Group>

      <Group>
        <SavedSearches filters={filters} onApply={onApply} />
      </Group>

      <Group title={t('filter.saleType')}>
        {/*
          Три кнопки, з яких активна завжди одна — це «радіо» без круглих
          позначок. aria-pressed усередині SaleType повідомляє програмам для
          незрячих, яка з них зараз натиснута.
        */}
        <div className="flex overflow-hidden rounded-control border border-line">
          <SaleType
            label={t('filter.all')}
            active={filters.type === undefined}
            onClick={() => onChange({ type: undefined })}
          />
          <SaleType
            label={t('filter.fixedPrice')}
            active={filters.type === 'FixedPrice'}
            onClick={() => onChange({ type: 'FixedPrice' })}
          />
          <SaleType
            label={t('filter.auction')}
            active={filters.type === 'Auction'}
            onClick={() => onChange({ type: 'Auction' })}
          />
        </div>
      </Group>

      <Group title={t('filter.condition')}>
        <Chips
          options={attributes.data?.conditions ?? []}
          // Стан один, не набір: авто або нове, або вживане. Chips вміє
          // множинний вибір, тож обмежуємо його тут — беремо останнє обране.
          selected={filters.condition ? [filters.condition] : []}
          onToggle={(chosen) => onChange({ condition: chosen.at(-1) })}
        />
      </Group>

      <Group title={t('filter.seller')}>
        {/*
          Тризначний вибір, а не прапорець: обидва боки однаково потрібні.
          Одні шукають гарантію салону, інші свідомо йдуть до приватника,
          щоб не переплачувати.
        */}
        <div className="flex overflow-hidden rounded-control border border-line">
          <SaleType
            label={t('filter.all')}
            active={filters.fromDealer === undefined}
            onClick={() => onChange({ fromDealer: undefined, verifiedDealerOnly: undefined })}
          />
          <SaleType
            label={t('filter.dealers')}
            active={filters.fromDealer === true}
            onClick={() => onChange({ fromDealer: true })}
          />
          <SaleType
            label={t('filter.private')}
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
            <span>{t('filter.verifiedOnly')}</span>
          </label>
        )}
      </Group>

      <Group title={t('filter.makeModel')}>
        <Select
          value={filters.makeId}
          placeholder={t('filter.anyMake')}
          options={(makes.data ?? []).map((make) => ({ id: make.id, name: make.name }))}
          // Модель належить марці, тож зміна марки скидає раніше обрану модель.
          onChange={(makeId) => onChange({ makeId, modelId: undefined })}
        />
        <Select
          value={filters.modelId}
          placeholder={filters.makeId ? t('filter.anyModel') : t('filter.chooseMakeFirst')}
          disabled={filters.makeId === undefined}
          options={(models.data ?? []).map((model) => ({ id: model.id, name: model.name }))}
          // Покоління належить моделі — зміна моделі скидає обране покоління.
          onChange={(modelId) => onChange({ modelId, generationId: undefined })}
        />

        {/*
          Покоління показуємо лише коли воно в моделі є. Порожній список
          «Будь-яке покоління» під кожною моделлю дратував би на порожньому
          місці — у більшості моделей поколінь у довіднику немає.
        */}
        {(generations.data?.length ?? 0) > 0 && (
          <Select
            value={filters.generationId}
            placeholder={t('filter.anyGeneration')}
            options={(generations.data ?? []).map((generation) => ({
              id: generation.id,
              name: generation.yearTo
                ? t('filter.generationRange', {
                    name: generation.name,
                    from: generation.yearFrom,
                    to: generation.yearTo,
                  })
                : t('filter.generationOpen', {
                    name: generation.name,
                    from: generation.yearFrom,
                  }),
            }))}
            onChange={(generationId) => onChange({ generationId })}
          />
        )}
      </Group>

      <Group title={t('filter.price')}>
        <div className="flex gap-2">
          <NumberInput
            value={filters.priceFrom}
            placeholder={t('filter.from')}
            onChange={(priceFrom) => onChange({ priceFrom })}
          />
          <NumberInput
            value={filters.priceTo}
            placeholder={t('filter.to')}
            onChange={(priceTo) => onChange({ priceTo })}
          />
          <CurrencyPicker
            value={filters.priceCurrency}
            onChange={(priceCurrency) => onChange({ priceCurrency })}
          />
        </div>
      </Group>

      <Group title={t('filter.year')}>
        <div className="flex gap-2">
          <NumberInput
            value={filters.yearFrom}
            placeholder={t('filter.from')}
            onChange={(yearFrom) => onChange({ yearFrom })}
          />
          <NumberInput
            value={filters.yearTo}
            placeholder={t('filter.to')}
            onChange={(yearTo) => onChange({ yearTo })}
          />
        </div>
      </Group>

      <Group title={t('filter.mileage')}>
        <div className="flex gap-2">
          <NumberInput
            value={filters.mileageFrom}
            placeholder={t('filter.from')}
            onChange={(mileageFrom) => onChange({ mileageFrom })}
          />
          <NumberInput
            value={filters.mileageTo}
            placeholder={t('filter.to')}
            onChange={(mileageTo) => onChange({ mileageTo })}
          />
        </div>
      </Group>

      <Group title={t('filter.engineVolume')}>
        <div className="flex gap-2">
          <NumberInput
            value={filters.engineVolumeFrom}
            placeholder={t('filter.from')}
            step="0.1"
            onChange={(engineVolumeFrom) => onChange({ engineVolumeFrom })}
          />
          <NumberInput
            value={filters.engineVolumeTo}
            placeholder={t('filter.to')}
            step="0.1"
            onChange={(engineVolumeTo) => onChange({ engineVolumeTo })}
          />
        </div>
      </Group>

      <Group title={t('filter.power')}>
        <div className="flex gap-2">
          <NumberInput
            value={filters.powerFrom}
            placeholder={t('filter.from')}
            onChange={(powerFrom) => onChange({ powerFrom })}
          />
          <NumberInput
            value={filters.powerTo}
            placeholder={t('filter.to')}
            onChange={(powerTo) => onChange({ powerTo })}
          />
        </div>
      </Group>

      <Group title={t('filter.fuel')}>
        <Chips
          options={attributes.data?.fuelTypes ?? []}
          selected={filters.fuelTypes}
          onToggle={(fuelTypes) => onChange({ fuelTypes })}
        />
      </Group>

      <Group title={t('filter.body')}>
        <Chips
          options={attributes.data?.bodyTypes ?? []}
          selected={filters.bodyTypes}
          onToggle={(bodyTypes) => onChange({ bodyTypes })}
        />
      </Group>

      <Group title={t('filter.transmission')}>
        <Chips
          options={attributes.data?.transmissions ?? []}
          selected={filters.transmissions}
          onToggle={(transmissions) => onChange({ transmissions })}
        />
      </Group>

      <Group title={t('filter.drivetrain')}>
        <Chips
          options={attributes.data?.driveTypes ?? []}
          selected={filters.drivetrains}
          onToggle={(drivetrains) => onChange({ drivetrains })}
        />
      </Group>

      <Group title={t('filter.colour')}>
        <Chips
          options={attributes.data?.colors ?? []}
          selected={filters.colors}
          onToggle={(colors) => onChange({ colors })}
        />
      </Group>

      <Group title={t('filter.features')}>
        <FeaturePicker
          selected={filters.featureIds}
          onChange={(featureIds) => onChange({ featureIds })}
        />
      </Group>

      <Group title={t('filter.region')}>
        <Select
          value={filters.regionId}
          placeholder={t('filter.wholeCountry')}
          options={regions.data ?? []}
          onChange={(regionId) => onChange({ regionId, cityId: undefined })}
        />
        <Select
          value={filters.cityId}
          placeholder={filters.regionId ? t('filter.allCities') : t('filter.chooseRegionFirst')}
          disabled={filters.regionId === undefined}
          options={cities.data ?? []}
          // Район належить місту — зміна міста скидає обраний район.
          onChange={(cityId) => onChange({ cityId, cityDistrictId: undefined })}
        />

        {/* Райони є лише у великих містах, тож і вибір показуємо лише там. */}
        {(cityDistricts.data?.length ?? 0) > 0 && (
          <Select
            value={filters.cityDistrictId}
            placeholder={t('filter.allDistricts')}
            options={cityDistricts.data ?? []}
            onChange={(cityDistrictId) => onChange({ cityDistrictId })}
          />
        )}
      </Group>

      <Group title={t('filter.origin')}>
        {/*
          Кожен прапорець тризначний за змістом: знята позначка означає
          «байдуже», а не «навпаки». Тому вимкнення повертає undefined,
          а не false — інакше знята «не був у ДТП» шукала б лише биті.
        */}
        <Toggle
          label={t('filter.noAccident')}
          checked={filters.wasInAccident === false}
          onChange={(on) => onChange({ wasInAccident: on ? false : undefined })}
        />
        <Toggle
          label={t('filter.customsCleared')}
          checked={filters.isCustomsCleared === true}
          onChange={(on) => onChange({ isCustomsCleared: on ? true : undefined })}
        />
        <Toggle
          label={t('filter.inUkraine')}
          checked={filters.isLocatedInUkraine === true}
          onChange={(on) => onChange({ isLocatedInUkraine: on ? true : undefined })}
        />
        <Toggle
          label={t('filter.withPhotos')}
          checked={filters.hasPhotos === true}
          onChange={(on) => onChange({ hasPhotos: on ? true : undefined })}
        />

        <Select
          value={filters.importedFromCountryId}
          placeholder={t('filter.importedFrom')}
          options={countries.data ?? []}
          onChange={(importedFromCountryId) => onChange({ importedFromCountryId })}
        />
      </Group>

      {/*
        Решта фільтрів під згортанням. Їх ще два десятки, і розгорнутими вони
        зробили б панель довшою за саму видачу — тим часом більшість людей
        шукає за маркою, ціною й роком. Хто шукає семимісний дизель із
        сервісною книжкою, той відкриє.
      */}
      <Group>
        <button
          type="button"
          onClick={() => setAdvanced((open) => !open)}
          className="flex w-full items-center gap-1.5 text-left text-[13px] text-accent hover:underline"
        >
          <span
            className={`transition-transform ${advanced ? 'rotate-90' : ''}`}
            aria-hidden="true"
          >
            &rsaquo;
          </span>
          {t('filter.advanced')}
        </button>
      </Group>

      {advanced && (
        <>
          <Group title={t('filter.consumption')}>
            <NumberInput
              value={filters.fuelConsumptionTo}
              placeholder={t('filter.atMost')}
              step="0.1"
              onChange={(fuelConsumptionTo) => onChange({ fuelConsumptionTo })}
            />
          </Group>

          <Group title={t('filter.owners')}>
            <NumberInput
              value={filters.ownerCountTo}
              placeholder={t('filter.atMost')}
              onChange={(ownerCountTo) => onChange({ ownerCountTo })}
            />
          </Group>

          <Group title={t('filter.seats')}>
            <div className="flex gap-2">
              <NumberInput
                value={filters.seatCountFrom}
                placeholder={t('filter.from')}
                onChange={(seatCountFrom) => onChange({ seatCountFrom })}
              />
              <NumberInput
                value={filters.seatCountTo}
                placeholder={t('filter.to')}
                onChange={(seatCountTo) => onChange({ seatCountTo })}
              />
            </div>
          </Group>

          <Group title={t('filter.doors')}>
            <NumberInput
              value={filters.doorCountFrom}
              placeholder={t('filter.from')}
              onChange={(doorCountFrom) => onChange({ doorCountFrom })}
            />
          </Group>

          {/*
            Блок електромобіля показуємо лише коли обрано відповідне пальне:
            запас ходу під бензиновим авто — просто шум.
          */}
          {isElectric(filters.fuelTypes) && (
            <>
              <Group title={t('filter.battery')}>
                <NumberInput
                  value={filters.batteryCapacityFrom}
                  placeholder={t('filter.from')}
                  step="0.1"
                  onChange={(batteryCapacityFrom) => onChange({ batteryCapacityFrom })}
                />
              </Group>

              <Group title={t('filter.range')}>
                <NumberInput
                  value={filters.electricRangeFrom}
                  placeholder={t('filter.from')}
                  onChange={(electricRangeFrom) => onChange({ electricRangeFrom })}
                />
              </Group>

              <Group title={t('filter.chargingPort')}>
                <Chips
                  options={attributes.data?.chargingPorts ?? []}
                  selected={filters.chargingPorts}
                  onToggle={(chargingPorts) => onChange({ chargingPorts })}
                />
              </Group>
            </>
          )}

          <Group title={t('filter.damage')}>
            <Chips
              options={attributes.data?.damageStates ?? []}
              selected={filters.damageStates}
              onToggle={(damageStates) => onChange({ damageStates })}
            />
          </Group>

          <Group title={t('filter.paint')}>
            <Chips
              options={attributes.data?.paintConditions ?? []}
              selected={filters.paintConditions}
              onToggle={(paintConditions) => onChange({ paintConditions })}
            />
          </Group>

          <Group title={t('filter.ecology')}>
            <Chips
              options={attributes.data?.ecologyStandards ?? []}
              selected={filters.ecologyStandards}
              onToggle={(ecologyStandards) => onChange({ ecologyStandards })}
            />
          </Group>

          <Group title={t('filter.manufacturerCountry')}>
            <Select
              value={filters.manufacturerCountryId}
              placeholder={t('filter.any')}
              options={countries.data ?? []}
              onChange={(manufacturerCountryId) => onChange({ manufacturerCountryId })}
            />
          </Group>

          <Group title={t('filter.terms')}>
            <Toggle
              label={t('filter.metallic')}
              checked={filters.isMetallic === true}
              onChange={(on) => onChange({ isMetallic: on ? true : undefined })}
            />
            <Toggle
              label={t('filter.serviceBook')}
              checked={filters.hasServiceBook === true}
              onChange={(on) => onChange({ hasServiceBook: on ? true : undefined })}
            />
            <Toggle
              label={t('filter.garageKept')}
              checked={filters.isGarageKept === true}
              onChange={(on) => onChange({ isGarageKept: on ? true : undefined })}
            />
            {/* Шукають саме НЕ кредитні, тож позначка вмикає false. */}
            <Toggle
              label={t('filter.notOnCredit')}
              checked={filters.isOnCredit === false}
              onChange={(on) => onChange({ isOnCredit: on ? false : undefined })}
            />
            <Toggle
              label={t('filter.negotiable')}
              checked={filters.isNegotiable === true}
              onChange={(on) => onChange({ isNegotiable: on ? true : undefined })}
            />
            <Toggle
              label={t('filter.acceptsTrade')}
              checked={filters.acceptsTrade === true}
              onChange={(on) => onChange({ acceptsTrade: on ? true : undefined })}
            />
            <Toggle
              label={t('filter.urgent')}
              checked={filters.isUrgent === true}
              onChange={(on) => onChange({ isUrgent: on ? true : undefined })}
            />
          </Group>
        </>
      )}

      <Group>
        <div className="btn btn-primary w-full">
          {t('filter.foundButton', { count: formatCount(totalCount) })}
        </div>
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

/**
 * Чи цікавлять людину електромобілі. Поля батареї показуємо лише тоді:
 * запас ходу під бензиновим авто — просто шум.
 */
function isElectric(fuelTypes: string[]): boolean {
  return fuelTypes.includes('Electric') || fuelTypes.includes('PluginHybrid')
}

/**
 * Прапорець фільтра. Окремим компонентом, бо їх чотири поспіль, і чотири
 * копії однакової розмітки розійшлися б при першій же правці вигляду.
 */
function Toggle({
  label,
  checked,
  onChange,
}: {
  label: string
  checked: boolean
  onChange: (checked: boolean) => void
}) {
  return (
    <label className="flex cursor-pointer items-center gap-2 text-[13.5px]">
      <input
        type="checkbox"
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
        // accent-accent фарбує саму «галочку» бірюзовим замість синього
        // кольору браузера за замовчуванням.
        className="h-[15px] w-[15px] accent-accent"
      />
      <span>{label}</span>
    </label>
  )
}

function NumberInput({
  value,
  placeholder,
  step,
  onChange,
}: {
  value: number | undefined
  placeholder: string

  /** Крок стрілок. Потрібен об'єму двигуна: він дробовий, решта — цілі. */
  step?: string
  onChange: (value: number | undefined) => void
}) {
  return (
    <input
      type="number"
      step={step}
      inputMode={step ? 'decimal' : 'numeric'}
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
