import { apiGet } from './client'
import type { DealerBadge } from './dealership'

/**
 * Типи, які приходять з бекенду назвами, а не числами: сервер налаштований
 * серіалізувати перелічення рядками, тож «Petrol» зі списку довідників
 * збігається з «Petrol» в оголошенні.
 */
export type Currency = 'Uah' | 'Usd' | 'Eur'
export type ListingType = 'FixedPrice' | 'Auction'

export interface ListingSummary {
  id: number
  title: string
  type: ListingType
  status: string
  price: number
  currency: Currency
  priceUah: number
  make: string
  model: string
  year: number
  mileage: number | null
  fuelType: string
  transmission: string
  cityName: string
  primaryPhotoPath: string | null
  publishedAt: string | null

  /** Чи відклав це оголошення той, хто зараз дивиться. Гість завжди бачить false. */
  isFavorite: boolean

  /** Салон, якщо продає він. Порожнє в приватної особи. */
  dealer: DealerBadge | null
}

export interface PagedResult<TItem> {
  items: TItem[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasPrevious: boolean
  hasNext: boolean
}

export type CatalogSort =
  | 'Newest'
  | 'PriceAscending'
  | 'PriceDescending'
  | 'MileageAscending'
  | 'YearDescending'

/** Стан форми фільтрів. Порожнє поле означає «не фільтрувати». */
export interface CatalogFilters {
  /** Слово в заголовку оголошення. */
  text?: string

  makeId?: number
  modelId?: number

  /** Покоління моделі. Має сенс лише разом із обраною моделлю. */
  generationId?: number
  priceFrom?: number
  priceTo?: number
  priceCurrency: Currency
  yearFrom?: number
  yearTo?: number
  mileageFrom?: number
  mileageTo?: number
  engineVolumeFrom?: number
  engineVolumeTo?: number
  powerFrom?: number
  powerTo?: number

  /** Витрата в змішаному циклі, л/100 км — «не більше». */
  fuelConsumptionTo?: number

  /** Скільки власників було — «не більше». */
  ownerCountTo?: number

  seatCountFrom?: number
  seatCountTo?: number
  doorCountFrom?: number

  // Три поля електромобіля. Фільтр «пальне = Електро» каже лише, що авто
  // електричне; покупця цікавить, скільки воно проїде й чим заряджається.
  batteryCapacityFrom?: number
  electricRangeFrom?: number
  chargingPorts: string[]
  fuelTypes: string[]
  bodyTypes: string[]
  transmissions: string[]
  drivetrains: string[]
  colors: string[]

  /** Новий чи вживаний. Порожньо — і ті, і ті. */
  condition?: string

  damageStates: string[]
  paintConditions: string[]
  ecologyStandards: string[]

  /** Металік. */
  isMetallic?: boolean

  regionId?: number
  cityId?: number

  /** Район міста. У великих містах відстань вирішує все. */
  cityDistrictId?: number
  type?: ListingType
  wasInAccident?: boolean

  /** Розмитнений. Для пригнаних це перше питання покупця. */
  isCustomsCleared?: boolean

  /** Уже в Україні, а не «під замовлення». */
  isLocatedInUkraine?: boolean

  /** Звідки пригнали. Порожньо — байдуже. */
  importedFromCountryId?: number

  /** Країна виробника. Це НЕ те саме, що «звідки пригнали». */
  manufacturerCountryId?: number

  hasServiceBook?: boolean
  isGarageKept?: boolean

  /** Найчастіше шукають ті, що НЕ в кредиті, тож значення тризначне. */
  isOnCredit?: boolean

  isNegotiable?: boolean
  acceptsTrade?: boolean
  isUrgent?: boolean

  /** Опції, які авто має мати ВСІ одразу, а не будь-яку з них. */
  featureIds: number[]

  /** Лише з фото: оголошення без жодного знімка зазвичай пропускають. */
  hasPhotos?: boolean

  /**
   * Хто продає: true — лише салони, false — лише приватні особи,
   * порожньо — усі. Обидва боки потрібні: одні шукають гарантію салону,
   * інші свідомо йдуть до приватника, щоб не переплачувати.
   */
  fromDealer?: boolean

  /** Лише салони з бейджем перевіреного. */
  verifiedDealerOnly?: boolean

  /** Вітрина конкретного салону. */
  dealershipId?: number

  sort: CatalogSort
  page: number
}

export const emptyFilters: CatalogFilters = {
  priceCurrency: 'Usd',
  fuelTypes: [],
  bodyTypes: [],
  transmissions: [],
  drivetrains: [],
  colors: [],
  damageStates: [],
  paintConditions: [],
  ecologyStandards: [],
  chargingPorts: [],
  featureIds: [],
  sort: 'Newest',
  page: 1,
}

/*
  Один перелік полів на обидва напрямки — і в адресний рядок, і назад.
  Раніше збирання жило окремо, а розбирання не існувало взагалі; щойно
  з'явилося друге, два переліки почали б розходитися при кожному новому
  фільтрі. Тепер новий фільтр, доданий сюди, одразу вміє і те, і те.

  Ключ об'єкта — ім'я параметра, як його чекає бекенд; значення — назва поля
  у фільтрах. Імена навмисно ті самі, що йдуть на сервер: тоді адреса в
  браузері й запит до API читаються однаково.
*/

const textParams = {
  Text: 'text',
  Condition: 'condition',
  PriceCurrency: 'priceCurrency',
  Type: 'type',
  Sort: 'sort',
} as const

const numberParams = {
  MakeId: 'makeId',
  ModelId: 'modelId',
  GenerationId: 'generationId',
  PriceFrom: 'priceFrom',
  PriceTo: 'priceTo',
  YearFrom: 'yearFrom',
  YearTo: 'yearTo',
  MileageFrom: 'mileageFrom',
  MileageTo: 'mileageTo',
  EngineVolumeFrom: 'engineVolumeFrom',
  EngineVolumeTo: 'engineVolumeTo',
  PowerFrom: 'powerFrom',
  PowerTo: 'powerTo',
  FuelConsumptionTo: 'fuelConsumptionTo',
  OwnerCountTo: 'ownerCountTo',
  SeatCountFrom: 'seatCountFrom',
  SeatCountTo: 'seatCountTo',
  DoorCountFrom: 'doorCountFrom',
  BatteryCapacityFrom: 'batteryCapacityFrom',
  ElectricRangeFrom: 'electricRangeFrom',
  RegionId: 'regionId',
  CityId: 'cityId',
  CityDistrictId: 'cityDistrictId',
  ImportedFromCountryId: 'importedFromCountryId',
  ManufacturerCountryId: 'manufacturerCountryId',
  DealershipId: 'dealershipId',
  Page: 'page',
} as const

const booleanParams = {
  IsMetallic: 'isMetallic',
  WasInAccident: 'wasInAccident',
  IsCustomsCleared: 'isCustomsCleared',
  IsLocatedInUkraine: 'isLocatedInUkraine',
  HasServiceBook: 'hasServiceBook',
  IsGarageKept: 'isGarageKept',
  IsOnCredit: 'isOnCredit',
  IsNegotiable: 'isNegotiable',
  AcceptsTrade: 'acceptsTrade',
  IsUrgent: 'isUrgent',
  HasPhotos: 'hasPhotos',
  FromDealer: 'fromDealer',
  VerifiedDealerOnly: 'verifiedDealerOnly',
} as const

/** Набори значень: ключ повторюється — FuelTypes=Diesel&FuelTypes=Electric. */
const listParams = {
  FuelTypes: 'fuelTypes',
  BodyTypes: 'bodyTypes',
  Transmissions: 'transmissions',
  Drivetrains: 'drivetrains',
  Colors: 'colors',
  DamageStates: 'damageStates',
  PaintConditions: 'paintConditions',
  EcologyStandards: 'ecologyStandards',
  ChargingPorts: 'chargingPorts',
} as const

/**
 * Збирає адресний рядок запиту. Порожні значення не додаються взагалі —
 * бекенд розрізняє «фільтр не вказаний» і «фільтр із порожнім значенням»,
 * і другий варіант зіпсував би видачу.
 */
export function toSearchParams(filters: CatalogFilters): URLSearchParams {
  const params = new URLSearchParams()

  const put = (name: string, value: unknown) => {
    if (value !== undefined && value !== null && value !== '') {
      params.append(name, String(value))
    }
  }

  for (const [name, key] of Object.entries(textParams)) put(name, filters[key])
  for (const [name, key] of Object.entries(numberParams)) put(name, filters[key])
  for (const [name, key] of Object.entries(booleanParams)) put(name, filters[key])

  for (const [name, key] of Object.entries(listParams)) {
    for (const value of filters[key]) params.append(name, value)
  }

  // Опції — числа, решта наборів рядкові. Ключ так само повторюється.
  for (const value of filters.featureIds) params.append('FeatureIds', String(value))

  return params
}

/**
 * Зворотний бік: відновлює фільтри з адресного рядка.
 *
 * Усе, чого не впізнали, просто ігнорується — адресу набирають руками, і
 * зіпсована літера в назві параметра не повинна валити сторінку. З тієї ж
 * причини числа, які не розібралися, лишаються порожніми: «MakeId=abc»
 * означає «марка не вказана», а не помилку.
 */
export function fromSearchParams(params: URLSearchParams): CatalogFilters {
  const filters: CatalogFilters = { ...emptyFilters }

  for (const [name, key] of Object.entries(textParams)) {
    const value = params.get(name)

    // Приведення тут неминуче: у фільтрах ці поля мають вужчі типи —
    // валюта, тип продажу, порядок. Значення з адреси перевіряє сервер,
    // і незнайоме він просто відхилить.
    if (value) (filters[key] as string) = value
  }

  for (const [name, key] of Object.entries(numberParams)) {
    const raw = params.get(name)

    // Порожній параметр («Page=») — це НЕ нуль: Number('') дає 0, і каталог
    // попросив би в сервера нульову сторінку. Порожнє означає «не вказано».
    if (raw === null || raw.trim() === '') continue

    const value = Number(raw)

    if (Number.isFinite(value)) {
      (filters[key] as number) = value
    }
  }

  for (const [name, key] of Object.entries(booleanParams)) {
    const value = params.get(name)

    if (value === 'true' || value === 'false') {
      (filters[key] as boolean) = value === 'true'
    }
  }

  for (const [name, key] of Object.entries(listParams)) {
    const values = params.getAll(name)

    if (values.length > 0) (filters[key] as string[]) = values
  }

  filters.featureIds = params
    .getAll('FeatureIds')
    .map(Number)
    .filter((id) => Number.isFinite(id) && id > 0)

  return filters
}

/**
 * Те саме, але для адреси в браузері: значення за замовчуванням прибрані.
 * «?Sort=Newest&Page=1» на чистому каталозі не несе змісту, а посилання
 * захаращує — і люди пересилають його одне одному саме таким.
 */
export function toBrowserParams(filters: CatalogFilters): URLSearchParams {
  const params = toSearchParams(filters)

  if (filters.sort === 'Newest') params.delete('Sort')
  if (filters.page === 1) params.delete('Page')

  // Валюта має сенс лише разом із межами ціни.
  if (filters.priceFrom === undefined && filters.priceTo === undefined) {
    params.delete('PriceCurrency')
  }

  return params
}

export function searchCatalog(
  filters: CatalogFilters,
  signal?: AbortSignal,
): Promise<PagedResult<ListingSummary>> {
  return apiGet<PagedResult<ListingSummary>>(
    `/api/catalog?${toSearchParams(filters).toString()}`,
    signal,
  )
}
