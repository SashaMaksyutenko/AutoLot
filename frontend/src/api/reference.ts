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
