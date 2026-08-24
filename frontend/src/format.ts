/**
 * Форматування чисел і дат для показу. Зібране в одному місці, щоб ціна
 * виглядала однаково скрізь, а не по-різному в кожному компоненті.
 */

const currencySigns: Record<string, string> = {
  Uah: '₴',
  Usd: '$',
  Eur: '€',
}

/** Нерозривний пробіл між тисячами: «14 200 $» не має ламатися на два рядки. */
const groups = new Intl.NumberFormat('uk-UA', { maximumFractionDigits: 0 })

export function formatPrice(amount: number, currency: string): string {
  return `${groups.format(amount)} ${currencySigns[currency] ?? currency}`
}

export function formatMileage(kilometres: number | null): string {
  return kilometres === null ? 'без пробігу' : `${groups.format(kilometres)} км`
}

export function formatCount(count: number): string {
  return groups.format(count)
}

/**
 * Українська форма множини: 1 оголошення, 2 оголошення, 5 оголошень.
 * Без цього видача писала б «знайдено 5 оголошення».
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
