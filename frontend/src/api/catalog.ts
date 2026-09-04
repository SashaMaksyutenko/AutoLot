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

/**
 * Збирає адресний рядок запиту. Порожні значення не додаються взагалі —
 * бекенд розрізняє «фільтр не вказаний» і «фільтр із порожнім значенням»,
 * і другий варіант зіпсував би видачу.
 */
export function toSearchParams(filters: CatalogFilters): URLSearchParams {
  const params = new URLSearchParams()

  const single: Record<string, string | number | boolean | undefined> = {
    Text: filters.text,
    MakeId: filters.makeId,
    ModelId: filters.modelId,
    GenerationId: filters.generationId,
    PriceFrom: filters.priceFrom,
    PriceTo: filters.priceTo,
    PriceCurrency: filters.priceCurrency,
    YearFrom: filters.yearFrom,
    YearTo: filters.yearTo,
    MileageFrom: filters.mileageFrom,
    MileageTo: filters.mileageTo,
    EngineVolumeFrom: filters.engineVolumeFrom,
    EngineVolumeTo: filters.engineVolumeTo,
    PowerFrom: filters.powerFrom,
    PowerTo: filters.powerTo,
    FuelConsumptionTo: filters.fuelConsumptionTo,
    OwnerCountTo: filters.ownerCountTo,
    SeatCountFrom: filters.seatCountFrom,
    SeatCountTo: filters.seatCountTo,
    DoorCountFrom: filters.doorCountFrom,
    BatteryCapacityFrom: filters.batteryCapacityFrom,
    ElectricRangeFrom: filters.electricRangeFrom,
    IsMetallic: filters.isMetallic,
    Condition: filters.condition,
    RegionId: filters.regionId,
    CityId: filters.cityId,
    CityDistrictId: filters.cityDistrictId,
    Type: filters.type,
    WasInAccident: filters.wasInAccident,
    IsCustomsCleared: filters.isCustomsCleared,
    IsLocatedInUkraine: filters.isLocatedInUkraine,
    ImportedFromCountryId: filters.importedFromCountryId,
    ManufacturerCountryId: filters.manufacturerCountryId,
    HasServiceBook: filters.hasServiceBook,
    IsGarageKept: filters.isGarageKept,
    IsOnCredit: filters.isOnCredit,
    IsNegotiable: filters.isNegotiable,
    AcceptsTrade: filters.acceptsTrade,
    IsUrgent: filters.isUrgent,
    HasPhotos: filters.hasPhotos,
    FromDealer: filters.fromDealer,
    VerifiedDealerOnly: filters.verifiedDealerOnly,
    DealershipId: filters.dealershipId,
    Sort: filters.sort,
    Page: filters.page,
  }

  for (const [key, value] of Object.entries(single)) {
    if (value !== undefined && value !== '') {
      params.append(key, String(value))
    }
  }

  // Набори повторюють ключ: FuelTypes=Diesel&FuelTypes=Electric.
  for (const value of filters.fuelTypes) params.append('FuelTypes', value)
  for (const value of filters.bodyTypes) params.append('BodyTypes', value)
  for (const value of filters.transmissions) params.append('Transmissions', value)
  for (const value of filters.drivetrains) params.append('Drivetrains', value)
  for (const value of filters.colors) params.append('Colors', value)
  for (const value of filters.damageStates) params.append('DamageStates', value)
  for (const value of filters.paintConditions) params.append('PaintConditions', value)
  for (const value of filters.ecologyStandards) params.append('EcologyStandards', value)
  for (const value of filters.chargingPorts) params.append('ChargingPorts', value)

  // Опції — числа, решта наборів рядкові. Ключ так само повторюється.
  for (const value of filters.featureIds) params.append('FeatureIds', String(value))

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
