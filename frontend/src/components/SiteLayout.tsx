import { Link, Outlet } from 'react-router-dom'

/** Спільна шапка для всіх сторінок; вміст підставляє маршрутизатор. */
export function SiteLayout() {
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
          <span>Увійти</span>
          <span className="rounded-sm bg-brand px-4 py-2 text-[13px] font-semibold text-white">
            Подати оголошення
          </span>
        </div>
      </header>

      <Outlet />
    </div>
  )
}
