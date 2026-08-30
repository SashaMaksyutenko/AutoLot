import type { DealerBadge } from './dealership'
import { apiGet } from './client'
import type { Currency, ListingType } from './catalog'

/** Одне фото галереї. `path` — повний розмір, `thumbnailPath` — зменшений. */
export interface ListingPhoto {
  id: number
  path: string
  thumbnailPath: string
  sortOrder: number
  isPrimary: boolean
}

/** Місцезнаходження, розгорнуте з міста в повну адресу. */
export interface UserLocation {
  regionId: number
  regionName: string
  districtId: number | null
  districtName: string | null
  cityId: number
  cityName: string
  cityDistrictId: number | null
  cityDistrictName: string | null
}

export interface SellerSummary {
  id: number
  displayName: string
  accountType: 'Private' | 'Dealer'

  /** Гість чужого номера не бачить: сервер його просто не віддає. */
  phoneNumber: string | null
}

/**
 * Характеристики для показу. Марка, модель, країни й опції приходять уже
 * назвами; перелічення — сирими значеннями, які треба перекласти довідником.
 */
export interface CarDetails {
  vin: string | null
  year: number
  condition: string
  make: string
  model: string
  generation: string | null
  mileage: number | null
  ownerCount: number | null
  fuelType: string
  engineVolume: number | null
  enginePower: number | null
  fuelConsumptionCity: number | null
  fuelConsumptionHighway: number | null
  fuelConsumptionCombined: number | null
  batteryCapacity: number | null
  electricRange: number | null
  chargingPort: string | null
  transmission: string
  drivetrain: string
  bodyType: string
  color: string
  isMetallic: boolean
  seatCount: number | null
  doorCount: number | null
  ecologyStandard: string | null
  manufacturerCountry: string | null
  importedFromCountry: string | null
  isCustomsCleared: boolean
  isLocatedInUkraine: boolean
  wasInAccident: boolean
  damageState: string
  paintCondition: string | null
  hasServiceBook: boolean
  isGarageKept: boolean
  isOnCredit: boolean
  features: string[]
}

export interface ListingDetails {
  id: number
  title: string
  description: string
  type: ListingType
  status: string
  price: number
  currency: Currency
  priceUah: number
  isNegotiable: boolean
  acceptsTrade: boolean
  isUrgent: boolean
  location: UserLocation | null
  seller: SellerSummary
  car: CarDetails
  photos: ListingPhoto[]
  publishedAt: string | null
  expiresAt: string | null
  rejectionReason: string | null
  viewCount: number

  /** Чи відклав це оголошення той, хто зараз дивиться. */
  isFavorite: boolean

  /** Салон, якщо продає він. */
  dealer: DealerBadge | null
}

export function fetchListing(id: number, signal?: AbortSignal): Promise<ListingDetails> {
  return apiGet<ListingDetails>(`/api/listings/${id}`, signal)
}
