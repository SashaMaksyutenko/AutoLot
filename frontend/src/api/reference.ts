import { apiGet } from './client'

/**
 * Довідники з бекенду. Назви приходять уже перекладеними — сервер обирає мову
 * із заголовка Accept-Language, тож клієнту не треба тримати власні словники
 * кузовів чи міст.
 */

/** Значення перелічення: `value` надсилаємо назад, `name` показуємо людині. */
export interface LookupItem {
  value: string
  name: string
}

export interface CarAttributes {
  bodyTypes: LookupItem[]
  fuelTypes: LookupItem[]
  transmissions: LookupItem[]
  driveTypes: LookupItem[]
  colors: LookupItem[]

  /** Новий чи вживаний. Теж довідник — назви перекладаються. */
  conditions: LookupItem[]

  damageStates: LookupItem[]
  paintConditions: LookupItem[]
  ecologyStandards: LookupItem[]

  /** Тип зарядного роз'єму. Має сенс лише для електромобілів. */
  chargingPorts: LookupItem[]
}

/** Опція комплектації всередині своєї категорії. */
export interface FeatureItem {
  id: number
  code: string
  name: string
}

/**
 * Опції, згруповані за категорією: «Комфорт», «Безпека», «Мультимедіа».
 * Плоский список із сотні позначок неможливо переглянути.
 */
export interface FeatureGroup {
  category: string
  features: FeatureItem[]
}

/** Покоління моделі: «B8 (2007–2015)». */
export interface GenerationItem {
  id: number
  name: string
  slug: string
  yearFrom: number
  yearTo: number | null
}

export interface MakeItem {
  id: number
  name: string
  slug: string
  isPopular: boolean
  modelCount: number
}

export interface ModelItem {
  id: number
  name: string
  slug: string
  hasGenerations: boolean
}

/** Рядок будь-якого географічного списку: область, місто, країна. */
export interface GeoItem {
  id: number
  name: string
}

export function fetchCarAttributes(signal?: AbortSignal): Promise<CarAttributes> {
  return apiGet<CarAttributes>('/api/cars/attributes', signal)
}

export function fetchMakes(signal?: AbortSignal): Promise<MakeItem[]> {
  return apiGet<MakeItem[]>('/api/cars/makes', signal)
}

export function fetchModels(makeId: number, signal?: AbortSignal): Promise<ModelItem[]> {
  return apiGet<ModelItem[]>(`/api/cars/makes/${makeId}/models`, signal)
}

export function fetchRegions(signal?: AbortSignal): Promise<GeoItem[]> {
  return apiGet<GeoItem[]>('/api/geo/regions', signal)
}

export function fetchCities(regionId: number, signal?: AbortSignal): Promise<GeoItem[]> {
  return apiGet<GeoItem[]>(`/api/geo/regions/${regionId}/cities`, signal)
}

export function fetchCityDistricts(
  cityId: number,
  signal?: AbortSignal,
): Promise<GeoItem[]> {
  return apiGet<GeoItem[]>(`/api/geo/cities/${cityId}/districts`, signal)
}

export function fetchCountries(signal?: AbortSignal): Promise<GeoItem[]> {
  return apiGet<GeoItem[]>('/api/geo/countries', signal)
}

export function fetchGenerations(
  modelId: number,
  signal?: AbortSignal,
): Promise<GenerationItem[]> {
  return apiGet<GenerationItem[]>(`/api/cars/models/${modelId}/generations`, signal)
}

export function fetchFeatures(signal?: AbortSignal): Promise<FeatureGroup[]> {
  return apiGet<FeatureGroup[]>('/api/cars/features', signal)
}
