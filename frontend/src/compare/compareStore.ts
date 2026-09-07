/**
 * Що людина відклала для порівняння.
 *
 * Живе поза React і в localStorage — як тема й токен. Причина не в зручності:
 * авто відкладають на різних сторінках, а порівнюють на четвертій, і вибір
 * має пережити і перехід, і випадкове перезавантаження.
 *
 * Свідомо НЕ на сервері: порівняння — це чернетка думки, а не дані акаунта.
 * Воно має працювати й гостю, який ще не реєструвався, і не має тягтися за
 * людиною на інший пристрій, де вона вибирає вже інше.
 */

/**
 * Скільки авто можна зіставити. Стільки ж перевіряє сервер.
 *
 * Чотири — це не смак: п'ята колонка не влазить на екран ноутбука, а таблиця
 * з горизонтальною прокруткою перестає виконувати єдину свою роботу —
 * показувати відмінності поруч.
 */
export const compareLimit = 4

const storageKey = 'autolot.compare'

let ids: number[] = readStored()

/** Підписники — компоненти, які треба перемалювати після зміни вибору. */
const listeners = new Set<() => void>()

/**
 * Знімок для useSyncExternalStore. React порівнює результат читання за
 * посиланням, тож повертати щоразу новий масив не можна — компонент
 * перемальовувався б нескінченно. Тому масив замінюємо лише при справжній
 * зміні, а тут просто віддаємо поточний.
 */
export function getCompared(): number[] {
  return ids
}

export function isCompared(listingId: number): boolean {
  return ids.includes(listingId)
}

/**
 * Додає або прибирає. Повертає false, якщо додати не вдалося через межу —
 * викликач тоді може пояснити людині, чому нічого не сталося.
 */
export function toggleCompared(listingId: number): boolean {
  if (ids.includes(listingId)) {
    save(ids.filter((id) => id !== listingId))

    return true
  }

  if (ids.length >= compareLimit) {
    return false
  }

  save([...ids, listingId])

  return true
}

export function clearCompared(): void {
  save([])
}

export function subscribeToCompared(listener: () => void): () => void {
  listeners.add(listener)

  return () => {
    listeners.delete(listener)
  }
}

function save(next: number[]): void {
  ids = next

  try {
    if (next.length === 0) {
      localStorage.removeItem(storageKey)
    } else {
      localStorage.setItem(storageKey, JSON.stringify(next))
    }
  } catch {
    // Приватний режим може заборонити localStorage — вибір усе одно
    // працюватиме до кінця сеансу.
  }

  listeners.forEach((listener) => listener())
}

function readStored(): number[] {
  try {
    const raw = localStorage.getItem(storageKey)

    if (raw === null) {
      return []
    }

    const parsed: unknown = JSON.parse(raw)

    // Вміст сховища писав не сервер, а браузер, і його міг зіпсувати хто
    // завгодно. Тому перевіряємо кожен елемент, а не довіряємо типу.
    if (!Array.isArray(parsed)) {
      return []
    }

    return parsed
      .filter((value): value is number => typeof value === 'number' && Number.isInteger(value))
      .slice(0, compareLimit)
  } catch {
    return []
  }
}
