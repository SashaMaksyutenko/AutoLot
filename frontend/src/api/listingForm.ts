import { apiGet, apiPost, apiPut } from './client'
import type { Currency, ListingType } from './catalog'

/**
 * Оголошення з боку автора: те, що форма надсилає, і те, що вона отримує
 * для редагування.
 *
 * Це НЕ те саме, що `ListingDetails` із `listing.ts`. Той тип описує картку
 * для показу й містить назви — «BMW», «Київ», «Підігрів сидінь». Форма ж
 * працює з ідентифікаторами довідників: саме їх вона надішле назад, і саме
 * за ними випадні списки знаходять обраний пункт.
 */

/** Характеристики авто. Однакові в обидва боки — сервер приймає рівно це. */
export interface CarSpecificationInput {
  vin?: string | null
  year: number
  condition: string
  makeId: number
  modelId: number
  generationId?: number | null
  mileage?: number | null
  ownerCount?: number | null
  fuelType: string
  engineVolume?: number | null
  enginePower?: number | null
  fuelConsumptionCity?: number | null
  fuelConsumptionHighway?: number | null
  fuelConsumptionCombined?: number | null
  batteryCapacity?: number | null
  electricRange?: number | null
  chargingPort?: string | null
  transmission: string
  drivetrain: string
  bodyType: string
  color: string
  isMetallic: boolean
  seatCount?: number | null
  doorCount?: number | null
  ecologyStandard?: string | null
  manufacturerCountryId?: number | null
  importedFromCountryId?: number | null
  isCustomsCleared: boolean
  isLocatedInUkraine: boolean
  wasInAccident: boolean
  damageState: string
  paintCondition?: string | null
  hasServiceBook: boolean
  isGarageKept: boolean
  isOnCredit: boolean
  featureIds: number[]
}

/** Те, що форма надсилає на сервер. */
export interface ListingInput {
  title: string
  description: string
  cityId: number
  cityDistrictId?: number | null
  price: number
  currency: Currency
  reservePrice?: number | null
  type: ListingType
  isNegotiable: boolean
  acceptsTrade: boolean
  isUrgent: boolean
  dealershipId?: number | null
  car: CarSpecificationInput
}

/**
 * Те, що приходить для редагування. Крім полів форми тут є область міста
 * (вибір міста двоступеневий) і стан оголошення з причиною відхилення —
 * форма має пояснити, ЧОМУ вона відкрилася.
 */
export interface ListingDraft extends ListingInput {
  id: number
  regionId: number
  status: string
  rejectionReason: string | null
}

export function createListing(input: ListingInput): Promise<{ id: number }> {
  return apiPost<{ id: number }>('/api/listings', input)
}

/**
 * Оновлення не приймає тип продажу й салон: перетворити оголошення з ціною
 * на аукціонний лот або передати його іншому салону — це не редагування, а
 * інша дія, і сервер таких полів у себе не чекає.
 */
export function updateListing(listingId: number, input: ListingInput): Promise<void> {
  return apiPut<void>(`/api/listings/${listingId}`, input)
}

export function fetchListingDraft(
  listingId: number,
  signal?: AbortSignal,
): Promise<ListingDraft> {
  return apiGet<ListingDraft>(`/api/listings/${listingId}/edit`, signal)
}
