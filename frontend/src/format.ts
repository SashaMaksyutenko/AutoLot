import { getLanguage, localeOf, type Language } from './i18n/languageStore'
import { translate } from './i18n/translate'

/**
 * Форматування чисел і дат для показу. Зібране в одному місці, щоб ціна
 * виглядала однаково скрізь, а не по-різному в кожному компоненті.
 *
 * Усе тут залежить від мови: українською тисячі відділяє пробіл
 * («381 024»), англійською — кома («381,024»), і дата теж пишеться інакше.
 * Тому кожен форматувальник питає поточну мову в момент виклику.
 */

const currencySigns: Record<string, string> = {
  Uah: '₴',
  Usd: '$',
  Eur: '€',
}

/**
 * Створення Intl-форматувальника коштує помітно дорожче за саме
 * форматування, а в списку з двадцяти карток його викликають сотні разів.
 * Тому кожен робимо один раз на мову й тримаємо в цій «шухляді».
 *
 * Partial<Record<…>> означає «ключі відомі, але значення може ще не бути»:
 * поки мовою не скористалися, її комірка порожня.
 */
function memoise<T>(create: (locale: string) => T): (language: Language) => T {
  const cache: Partial<Record<Language, T>> = {}

  return (language) => (cache[language] ??= create(localeOf(language)))
}

/** Нерозривний пробіл між тисячами: «14 200 $» не має ламатися на два рядки. */
const groups = memoise(
  (locale) => new Intl.NumberFormat(locale, { maximumFractionDigits: 0 }),
)

/**
 * Дата й час у місцевому поясі того, хто дивиться. Сервер віддає час із
 * поясом, а Intl сам переводить його в той, що на пристрої, — тож писати
 * «17:04» киянину й «16:04» варшав'янину не доводиться вручну.
 */
const dateTime = memoise(
  (locale) =>
    new Intl.DateTimeFormat(locale, {
      day: 'numeric',
      month: 'long',
      hour: '2-digit',
      minute: '2-digit',
    }),
)

const monthYear = memoise(
  (locale) => new Intl.DateTimeFormat(locale, { month: 'long', year: 'numeric' }),
)

export function formatPrice(amount: number, currency: string): string {
  return `${groups(getLanguage()).format(amount)} ${currencySigns[currency] ?? currency}`
}

export function formatMileage(kilometres: number | null): string {
  return kilometres === null
    ? translate('format.noMileage')
    : `${groups(getLanguage()).format(kilometres)} км`
}

export function formatCount(count: number): string {
  return groups(getLanguage()).format(count)
}

export function formatDateTime(iso: string): string {
  return dateTime(getLanguage()).format(new Date(iso))
}

/**
 * «вересень 2026» — для «на AutoLot із…». Точний день там зайвий: важить
 * порядок величини, а не дата.
 */
export function formatMonthYear(iso: string): string {
  return monthYear(getLanguage()).format(new Date(iso))
}

/**
 * Українська форма множини: 1 оголошення, 2 оголошення, 5 оголошень.
 * Без цього видача писала б «знайдено 5 оголошення».
 *
 * ТИМЧАСОВА функція: перекладені екрани користуються translatePlural із
 * i18n, який знає правила обох мов від Intl. Ця лишається доти, доки не
 * перекладено решту екранів (пункт 17б плану) — там текст поки лише
 * український, і вигадувати для нього форми англійською нема з чого.
 */
export function plural(count: number, one: string, few: string, many: string): string {
  const mod100 = count % 100

  if (mod100 >= 11 && mod100 <= 14) return many

  switch (count % 10) {
    case 1:
      return one
    case 2:
    case 3:
    case 4:
      return few
    default:
      return many
  }
}
