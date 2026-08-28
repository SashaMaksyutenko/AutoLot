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
  makeId?: number
  modelId?: number
  priceFrom?: number
  priceTo?: number
  priceCurrency: Currency
  yearFrom?: number
  yearTo?: number
  mileageTo?: number
  fuelTypes: string[]
  bodyTypes: string[]
  transmissions: string[]
  regionId?: number
  cityId?: number
  type?: ListingType
  wasInAccident?: boolean

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
    MakeId: filters.makeId,
    ModelId: filters.modelId,
    PriceFrom: filters.priceFrom,
    PriceTo: filters.priceTo,
    PriceCurrency: filters.priceCurrency,
    YearFrom: filters.yearFrom,
    YearTo: filters.yearTo,
    MileageTo: filters.mileageTo,
    RegionId: filters.regionId,
    CityId: filters.cityId,
    Type: filters.type,
    WasInAccident: filters.wasInAccident,
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
