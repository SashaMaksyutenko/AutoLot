import { apiGet } from './client'

/**
 * Ринкові ціни.
 *
 * Усі суми в гривні: оголошення виставляють у трьох валютах, і без спільної
 * одиниці «середня ціна» була б сумою доларів із гривнями.
 *
 * Сервер відповідає 204, коли оголошень надто мало, щоб щось виводити. Це не
 * помилка, а чесне «сказати нічого», і клієнт має мовчати так само.
 */

/** На якій вибірці порахували ціну. */
export type PriceBasis = 'ModelAndYear' | 'Model'

export interface PriceStats {
  /** Розмір вибірки. Показуємо завжди — «по трьох» і «по трьохстах» різні за вагою. */
  count: number
  basis: PriceBasis
  makeName: string
  modelName: string

  /** Рік, якщо вибірка саме по ньому. Порожній для всієї моделі. */
  year: number | null

  /** Медіана — «типова» ціна. Головне число саме вона, а не середнє. */
  median: number
  average: number
  min: number
  max: number
}

export interface PriceInsight {
  market: PriceStats
  priceUah: number

  /** Від медіани, у відсотках. Від'ємне — дешевше за ринок. */
  percentFromMedian: number
}

/**
 * Ціна оголошення на тлі ринку. `null`, якщо порівнювати нема з чим.
 *
 * На 204 `apiGet` віддає undefined — саме так сервер каже «вибірка замала».
 * Зводимо це до null тут, щоб компонент не знав ні про коди відповіді, ні
 * про два різні способи сказати «нічого».
 */
export async function fetchPriceInsight(
  listingId: number,
  signal?: AbortSignal,
): Promise<PriceInsight | null> {
  const result = await apiGet<PriceInsight | undefined>(
    `/api/analytics/listings/${listingId}/price`,
    signal,
  )

  return result ?? null
}
