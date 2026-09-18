import { apiGet } from './client'

/** Одна точка на графіку: скільки коштувало авто й від якого дня. */
export interface PriceHistoryPoint {
  readonly price: number
  readonly currency: string

  /**
   * Та сама сума в гривні. Малювати лінію треба саме за нею: продавець міг
   * змінити валюту, і без спільної одиниці графік стрибав би там, де ціна
   * насправді не рухалася.
   */
  readonly priceUah: number
  readonly changedAt: string
}

export function fetchPriceHistory(
  listingId: number,
  signal?: AbortSignal,
): Promise<PriceHistoryPoint[]> {
  return apiGet<PriceHistoryPoint[]>(`/api/listings/${listingId}/price-history`, signal)
}
