import { useState } from 'react'
import { Link, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { AuthDialog } from './AuthDialog'

/** Спільна шапка для всіх сторінок; вміст підставляє маршрутизатор. */
export function SiteLayout() {
  const auth = useAuth()
  const [authOpen, setAuthOpen] = useState(false)

  return (
    <div className="min-h-dvh">
      <header className="flex h-15 items-center gap-8 border-b border-line bg-surface px-10">
        <Link to="/" className="text-[19px] font-bold tracking-tight text-ink">
          Auto<span className="text-brand">Lot</span>
        </Link>
        <nav className="flex gap-6 text-sm font-medium text-muted">
          <Link to="/" className="text-ink">
            Каталог
          </Link>
          <span>Аукціони</span>
          <span>Дилери</span>
        </nav>

        <div className="flex-grow" />

        <div className="flex items-center gap-4 text-[13px] text-muted">
          {/* Поки поновлюється сесія, не показуємо ні «Увійти», ні ім'я:
              інакше на секунду блимнуло б «Увійти» вже залогіненому. */}
          {auth.isRestoring ? null : auth.user ? (
            <>
              <span className="font-medium text-ink">{auth.user.displayName}</span>
              <button type="button" onClick={() => void auth.logout()} className="hover:text-brand">
                Вийти
              </button>
              <span className="rounded-sm bg-brand px-4 py-2 text-[13px] font-semibold text-white">
                Подати оголошення
              </span>
            </>
          ) : (
            <>
              <button type="button" onClick={() => setAuthOpen(true)} className="hover:text-brand">
                Увійти
              </button>
              <button
                type="button"
                onClick={() => setAuthOpen(true)}
                className="rounded-sm bg-brand px-4 py-2 text-[13px] font-semibold text-white"
              >
                Подати оголошення
              </button>
            </>
          )}
        </div>
      </header>

      <Outlet />

      {authOpen && <AuthDialog onClose={() => setAuthOpen(false)} />}
    </div>
  )
}
