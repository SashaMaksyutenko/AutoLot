/**
 * Обрана мова інтерфейсу. Живе поза React — рівно з тих самих причин, що й
 * тема: мова потрібна ще до першого малювання, її читає код, який до React
 * не має стосунку (обгортка над fetch), і джерело істини має бути одне.
 */

export type Language = 'uk' | 'en'

/** Мова, якою говоримо, поки людина не обрала іншої. */
const fallback: Language = 'uk'

const storageKey = 'autolot.language'

let current: Language = readInitialLanguage()

/**
 * Підписники — компоненти, які треба перемалювати після зміни мови.
 * Set, а не масив: та сама функція додається один раз і швидко видаляється.
 */
const listeners = new Set<() => void>()

applyToDocument(current)

function isLanguage(value: unknown): value is Language {
  return value === 'uk' || value === 'en'
}

/**
 * Порядок пошуку: спершу власний вибір людини, потім налаштування браузера,
 * і лише тоді типова мова.
 *
 * navigator.languages — список мов у порядку переваги («uk-UA», «en-US», …).
 * Беремо перший збіг: якщо браузер англійський, немає сенсу зустрічати
 * людину українською тільки тому, що майданчик український.
 */
function readInitialLanguage(): Language {
  try {
    const stored = localStorage.getItem(storageKey)

    if (isLanguage(stored)) return stored
  } catch {
    // Приватний режим браузера може заборонити localStorage — не привід падати.
  }

  for (const tag of navigator.languages ?? [navigator.language]) {
    // «uk-UA» → «uk»: нас цікавить мова, а не країна.
    const code = tag.split('-')[0].toLowerCase()

    if (isLanguage(code)) return code
  }

  return fallback
}

/**
 * Атрибут lang на <html> — не косметика. За ним браузер обирає правила
 * переносу слів, а програми для незрячих — вимову: англійський текст,
 * прочитаний українським голосом, нерозбірливий.
 */
function applyToDocument(value: Language): void {
  document.documentElement.lang = value
}

export function getLanguage(): Language {
  return current
}

export function setLanguage(value: Language): void {
  if (value === current) return

  current = value
  applyToDocument(value)

  try {
    localStorage.setItem(storageKey, value)
  } catch {
    // Не змогли запам'ятати — вибір усе одно діє до кінця сеансу.
  }

  listeners.forEach((listener) => listener())
}

/**
 * Повертає функцію відписки. Такий вигляд вимагає React: він викликає її,
 * коли компонент зникає з екрана, щоб не тримати мертвих підписників.
 */
export function subscribeToLanguage(listener: () => void): () => void {
  listeners.add(listener)

  return () => {
    listeners.delete(listener)
  }
}
