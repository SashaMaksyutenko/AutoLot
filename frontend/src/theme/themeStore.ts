/**
 * Стан теми оформлення. Живе поза React — як і сховище токена — бо тема
 * потрібна ще до першого малювання сторінки й змінюється з одного місця.
 *
 * Станів три:
 *   'system' — жодного вибору не зроблено, слухаємо налаштування системи;
 *   'light' / 'dark' — людина натиснула перемикач, її слово головніше.
 */

export type ThemeChoice = 'system' | 'light' | 'dark'

/** Те, що бачить око: 'system' сюди не потрапляє, він уже розгорнутий. */
export type EffectiveTheme = 'light' | 'dark'

const storageKey = 'autolot.theme'

/**
 * matchMedia дає доступ до CSS-запиту з коду: .matches каже, чи система
 * зараз у темному режимі, а подія change спрацьовує, коли людина перемикає
 * тему Windows, не перезавантажуючи сторінку.
 */
const systemPrefersDark = window.matchMedia('(prefers-color-scheme: dark)')

let choice: ThemeChoice = readStoredChoice()

/**
 * Підписники — це компоненти, які треба перемалювати після зміни теми.
 * Set, а не масив: те саме додається один раз і швидко видаляється.
 */
const listeners = new Set<() => void>()

systemPrefersDark.addEventListener('change', () => {
  // Системна зміна цікавить нас лише поки власного вибору немає.
  if (choice === 'system') notifyListeners()
})

function readStoredChoice(): ThemeChoice {
  try {
    const stored = localStorage.getItem(storageKey)

    return stored === 'light' || stored === 'dark' ? stored : 'system'
  } catch {
    // Приватний режим браузера може заборонити localStorage — це не привід падати.
    return 'system'
  }
}

/**
 * Атрибут data-theme на <html> — те єдине, що бачить CSS. Коли вибору немає,
 * атрибут прибираємо повністю, і тоді спрацьовує медіазапит із index.css.
 */
function applyToDocument(value: ThemeChoice): void {
  if (value === 'system') {
    delete document.documentElement.dataset.theme
  } else {
    document.documentElement.dataset.theme = value
  }
}

function notifyListeners(): void {
  listeners.forEach((listener) => listener())
}

export function getEffectiveTheme(): EffectiveTheme {
  if (choice !== 'system') return choice

  return systemPrefersDark.matches ? 'dark' : 'light'
}

export function setTheme(value: ThemeChoice): void {
  choice = value
  applyToDocument(value)

  try {
    if (value === 'system') {
      localStorage.removeItem(storageKey)
    } else {
      localStorage.setItem(storageKey, value)
    }
  } catch {
    // Не змогли запам'ятати — тема все одно застосована до кінця сеансу.
  }

  notifyListeners()
}

/**
 * Повертає функцію відписки. Такий вигляд вимагає React: він викликає її,
 * коли компонент зникає з екрана, щоб не тримати мертвих підписників.
 */
export function subscribeToTheme(listener: () => void): () => void {
  listeners.add(listener)

  return () => {
    listeners.delete(listener)
  }
}
