import { useTheme } from '../theme/useTheme'

/**
 * Перемикач світлої й темної теми. Показує піктограму того, НА ЩО перемкне:
 * у світлій темі — місяць, у темній — сонце. Так зрозуміліше, що станеться
 * після натискання.
 */
export function ThemeToggle() {
  const { theme, toggle } = useTheme()
  const goingDark = theme === 'light'

  return (
    <button
      type="button"
      onClick={toggle}
      // title спливає підказкою для миші, aria-label читає програма для
      // незрячих: у кнопки немає тексту, лише картинка.
      title={goingDark ? 'Темна тема' : 'Світла тема'}
      aria-label={goingDark ? 'Увімкнути темну тему' : 'Увімкнути світлу тему'}
      className="grid h-[30px] w-[32px] shrink-0 place-items-center rounded-control border border-line bg-surface-2 text-ink-2 hover:text-ink"
    >
      {goingDark ? <MoonIcon /> : <SunIcon />}
    </button>
  )
}

/*
  Піктограми намальовані просто в коді, а не взяті файлами: дві дрібні
  картинки не варті ані окремих запитів, ані сторонньої бібліотеки.
  currentColor означає «той самий колір, що й текст кнопки», тож піктограма
  сама змінює відтінок при наведенні й у темній темі.
*/

function SunIcon() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="12" cy="12" r="4.2" fill="currentColor" />
      <g stroke="currentColor" strokeWidth="1.8" strokeLinecap="round">
        <path d="M12 2.4v2.6M12 19v2.6M2.4 12h2.6M19 12h2.6" />
        <path d="M5.2 5.2l1.9 1.9M16.9 16.9l1.9 1.9M18.8 5.2l-1.9 1.9M7.1 16.9l-1.9 1.9" />
      </g>
    </svg>
  )
}

function MoonIcon() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" aria-hidden="true">
      {/* Серп: коло з «викушеним» другим колом — так місяць малюють одним контуром. */}
      <path
        d="M20.5 14.3A8.6 8.6 0 0 1 9.7 3.5a8.6 8.6 0 1 0 10.8 10.8z"
        fill="currentColor"
      />
    </svg>
  )
}
