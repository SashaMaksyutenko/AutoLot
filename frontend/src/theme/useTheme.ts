import { useSyncExternalStore } from 'react'
import { getEffectiveTheme, setTheme, subscribeToTheme, type EffectiveTheme } from './themeStore'

interface ThemeControls {
  /** Тема, яку людина бачить просто зараз. */
  theme: EffectiveTheme
  /** Перемикає на протилежну й запам'ятовує вибір. */
  toggle: () => void
}

/**
 * useSyncExternalStore — вбудований гачок React для стану, що живе ЗА межами
 * React. Йому дають дві функції: як підписатися на зміни й як прочитати
 * поточне значення. React сам перемалює компонент, коли сховище повідомить
 * про зміну, і гарантує, що всі компоненти побачать те саме значення.
 *
 * Через це не потрібен ані провайдер, ані контекст: скільки б місць не
 * питали тему, вони читають одне спільне сховище.
 */
export function useTheme(): ThemeControls {
  const theme = useSyncExternalStore(subscribeToTheme, getEffectiveTheme)

  return {
    theme,

    // Натиснувши перемикач, людина робить вибір явним, тож зі стану 'system'
    // ми виходимо назавжди — далі діє лише те, що вона обрала.
    toggle: () => setTheme(theme === 'dark' ? 'light' : 'dark'),
  }
}
