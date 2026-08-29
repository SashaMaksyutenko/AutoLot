import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../../auth/useAuth'

/**
 * Спільна оболонка адмінки: бічне меню й місце для розділу.
 *
 * Розділи показуємо за ролями, і це не лише зручність. Модератор працює з
 * оголошеннями, адміністратор — з людьми; бекенд розділяє їх так само, тож
 * показувати модераторові розділ, куди його не пустять, було б обманом.
 */
export function AdminLayout() {
  const auth = useAuth()

  const roles = auth.user?.roles ?? []
  const isAdmin = roles.includes('Admin')
  const isModerator = isAdmin || roles.includes('Moderator')

  if (auth.isRestoring) {
    return <Notice>Завантажуємо…</Notice>
  }

  if (!isModerator) {
    return <Notice>Цей розділ доступний лише модераторам і адміністраторам.</Notice>
  }

  return (
    <div className="wrap grid items-start gap-[22px] py-[26px] lg:grid-cols-[218px_minmax(0,1fr)]">
      <nav className="card grid gap-0.5 self-start p-2 lg:sticky lg:top-[74px]">
        <Item to="/admin" label="Огляд" end />
        {isModerator && <Item to="/admin/queue" label="Черга модерації" />}
        {isAdmin && <Item to="/admin/users" label="Користувачі" />}
      </nav>

      <main className="grid min-w-0 gap-4">
        <Outlet />
      </main>
    </div>
  )
}

function Item({ to, label, end }: { to: string; label: string; end?: boolean }) {
  return (
    <NavLink
      to={to}
      end={end}
      className={({ isActive }) =>
        `rounded-control px-3 py-2 text-[13.5px] ${
          isActive ? 'bg-accent-soft font-semibold text-accent' : 'text-ink-2 hover:bg-surface-2'
        }`
      }
    >
      {label}
    </NavLink>
  )
}

function Notice({ children }: { children: React.ReactNode }) {
  return (
    <div className="wrap py-16">
      <p className="card mx-auto max-w-[460px] p-10 text-center text-sm text-ink-2">{children}</p>
    </div>
  )
}
