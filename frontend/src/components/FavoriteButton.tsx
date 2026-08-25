import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { addFavorite, removeFavorite } from '../api/favorites'
import { useAuth } from '../auth/useAuth'
import { openSignIn } from '../auth/signInPrompt'

interface Props {
  listingId: number
  /** Стан, який прийшов з сервера разом з оголошенням. */
  isFavorite: boolean
  /** Велике сердечко для сторінки авто, дрібне — для картки у видачі. */
  size?: 'small' | 'large'
}

/**
 * Сердечко «відкласти». Гостю воно теж показується — інакше про обране
 * ніхто б не дізнався, поки не зареєструється; натискання просто відкриває
 * вікно входу.
 */
export function FavoriteButton({ listingId, isFavorite, size = 'small' }: Props) {
  const auth = useAuth()
  const queryClient = useQueryClient()

  /*
    Стан тримаємо тут, а не перечитуємо з сервера: сердечко має
    зафарбуватися миттєво, ще до того, як запит долетить. Початкове
    значення — те, що прийшло з оголошенням.
  */
  const [active, setActive] = useState(isFavorite)

  const toggle = useMutation({
    mutationFn: (next: boolean) => (next ? addFavorite(listingId) : removeFavorite(listingId)),

    onSuccess: () => {
      // Лічильник у шапці й сама сторінка обраного тепер застарілі —
      // просимо TanStack Query перечитати їх при першій же потребі.
      void queryClient.invalidateQueries({ queryKey: ['favorite-count'] })
      void queryClient.invalidateQueries({ queryKey: ['favorites'] })
    },

    // Запит не пройшов — повертаємо сердечко в попередній вигляд, щоб
    // людина не думала, що авто відкладене, коли насправді ні.
    onError: (_error, next) => setActive(!next),
  })

  function click(event: React.MouseEvent) {
    // Сердечко часто лежить усередині посилання на оголошення: без цього
    // натискання відкривало б картку авто замість того, щоб відкласти його.
    event.preventDefault()
    event.stopPropagation()

    if (!auth.user) {
      openSignIn()
      return
    }

    const next = !active
    setActive(next)
    toggle.mutate(next)
  }

  const box = size === 'large' ? 'h-10 w-10' : 'h-8 w-8'
  const glyph = size === 'large' ? 20 : 16

  return (
    <button
      type="button"
      onClick={click}
      aria-pressed={active}
      title={active ? 'Прибрати з обраного' : 'Додати в обране'}
      aria-label={active ? 'Прибрати з обраного' : 'Додати в обране'}
      className={`${box} grid shrink-0 place-items-center rounded-control border backdrop-blur-sm transition ${
        active
          ? 'border-signal bg-signal-soft text-signal'
          : 'border-line bg-surface/85 text-ink-3 hover:text-ink'
      }`}
    >
      <HeartIcon filled={active} size={glyph} />
    </button>
  )
}

/**
 * Одне й те саме серце в двох станах: залите й порожнє. fill="none" плюс
 * обведення дає контур, fill="currentColor" — суцільну фігуру.
 */
function HeartIcon({ filled, size }: { filled: boolean; size: number }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill={filled ? 'currentColor' : 'none'}
      stroke="currentColor"
      strokeWidth={filled ? 0 : 1.9}
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M12 20.3 4.6 13a4.8 4.8 0 0 1 0-6.8 4.8 4.8 0 0 1 6.8 0l.6.6.6-.6a4.8 4.8 0 0 1 6.8 0 4.8 4.8 0 0 1 0 6.8z" />
    </svg>
  )
}
