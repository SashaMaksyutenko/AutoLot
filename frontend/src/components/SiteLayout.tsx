import { Link, NavLink, Outlet, useLocation, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { fetchUnreadCount } from '../api/chat'
import { fetchFavoriteCount } from '../api/favorites'
import { useAuth } from '../auth/useAuth'
import { useCompare } from '../compare/useCompare'
import { closeSignIn, openSignIn, useSignInPrompt } from '../auth/signInPrompt'
import { useTranslation } from '../i18n/useTranslation'
import { AuthDialog } from './AuthDialog'
import { LanguageToggle } from './LanguageToggle'
import { ThemeToggle } from './ThemeToggle'

/** Спільна шапка для всіх сторінок; вміст підставляє маршрутизатор. */
export function SiteLayout() {
  const { t } = useTranslation()
  const auth = useAuth()

  /*
    Окремої сторінки аукціонів немає — це той самий каталог із фільтром.
    Тому підсвічування рахуємо самі: NavLink дивиться лише на шлях, і на
    «/» він вважав би активними обидві вкладки одразу.
  */
  const [params] = useSearchParams()
  const onCatalog = useLocation().pathname === '/'
  const auctionsOpen = onCatalog && params.get('Type') === 'Auction'
  const { ids: compared } = useCompare()

  // Вікно входу відкриває не лише кнопка в шапці, а й, скажімо, сердечко
  // на картці, яке натиснув гість. Тому його стан живе в спільному сховищі.
  const authOpen = useSignInPrompt()

  return (
    <div className="min-h-dvh">
      {/*
        sticky top-0 лишає шапку на екрані під час прокрутки. z-40 піднімає її
        над вмістом, інакше картки проїжджали б поверх шапки.
      */}
      <header className="sticky top-0 z-40 border-b border-line bg-surface">
        <div className="wrap flex min-h-[62px] flex-wrap items-center gap-x-7 gap-y-2 py-2">
          <Brand />

          {/* mr-auto відтісняє все наступне до правого краю. */}
          <nav className="mr-auto hidden gap-5 text-[14.5px] sm:flex">
            <NavItem to="/" label={t('nav.buy')} active={onCatalog && !auctionsOpen} />
            {/*
              Порівняння доступне й гостю: воно живе в браузері, а не в
              акаунті. Показуємо пункт лише коли є що порівнювати — порожній
              він тільки додавав би шуму в шапку.
            */}
            {compared.length > 0 && (
              <NavItem
                to="/compare"
                label={t('nav.compare')}
                badge={
                  <span className="rounded-full bg-accent-soft px-1.5 font-mono text-[11px] font-semibold text-accent tabular-nums">
                    {compared.length}
                  </span>
                }
              />
            )}
            {auth.user && <NavItem to="/favorites" label={t('nav.favorites')} badge={<FavoriteBadge />} />}
            {auth.user && <NavItem to="/chat" label={t('nav.messages')} badge={<ChatBadge />} />}
            <NavItem to="/dealers" label={t('nav.dealers')} />
            {/* Адмінку показуємо лише тим, кого туди пустять. */}
            {isStaff(auth) && <NavItem to="/admin" label={t('nav.admin')} />}
            <NavItem to="/?Type=Auction" label={t('nav.auctions')} active={auctionsOpen} />
          </nav>

          <div className="flex flex-wrap items-center gap-2.5">
            <LanguageToggle />
            <ThemeToggle />
            <AccountTools auth={auth} />
          </div>
        </div>
      </header>

      <Outlet />

      {authOpen && <AuthDialog onClose={closeSignIn} />}
    </div>
  )
}

/**
 * NavLink від react-router сам знає, чи веде він на поточну сторінку, і
 * передає це прапорцем isActive — інакше довелося б щоразу порівнювати
 * адресу вручну.
 */
function NavItem({
  to,
  label,
  badge,
  active,
}: {
  to: string
  label: string
  badge?: React.ReactNode

  /** Підсвічування ззовні — для вкладок, що різняться лише фільтром. */
  active?: boolean
}) {
  return (
    <NavLink
      to={to}
      end
      className={({ isActive }) =>
        `flex items-center gap-1.5 border-b-2 pb-1 ${
          (active ?? isActive)
            ? 'border-accent text-ink'
            : 'border-transparent text-ink-2 hover:text-ink'
        }`
      }
    >
      {label}
      {badge}
    </NavLink>
  )
}

/** Скільки непрочитаних повідомлень. Нуль не показуємо взагалі. */
function ChatBadge() {
  const count = useQuery({
    queryKey: ['chat-unread'],
    queryFn: ({ signal }) => fetchUnreadCount(signal),
  })

  if (!count.data?.count) return null

  return (
    <span className="pill pill-live tabular-nums">{count.data.count}</span>
  )
}

/** Скільки оголошень у обраному. Порожній список не показуємо взагалі. */
function FavoriteBadge() {
  const count = useQuery({
    queryKey: ['favorite-count'],
    queryFn: ({ signal }) => fetchFavoriteCount(signal),
  })

  if (!count.data?.count) return null

  return (
    <span className="rounded-full bg-accent-soft px-1.5 font-mono text-[11px] font-semibold text-accent tabular-nums">
      {count.data.count}
    </span>
  )
}

function Brand() {
  return (
    <Link to="/" className="flex items-center gap-2.5">
      <span className="font-display grid h-[30px] w-[30px] place-items-center rounded-control bg-accent text-sm font-bold tracking-wider text-accent-ink">
        AL
      </span>
      <span className="font-display text-[21px] font-bold">
        AUTO
        {/*
          Підкреслення сигнальним кольором — єдиний помаранчевий штрих у шапці.
          not-italic скасовує курсив, який <em> дає за замовчуванням: тег тут
          потрібен заради змісту (виділена частина назви), а не заради нахилу.
        */}
        <em className="border-b-[3px] border-signal pb-px text-accent not-italic">LOT</em>
      </span>
    </Link>
  )
}

function AccountTools({ auth }: { auth: ReturnType<typeof useAuth> }) {
  const { t } = useTranslation()

  // Поки поновлюється сесія, не показуємо ні «Увійти», ні ім'я: інакше
  // на секунду блимнуло б «Увійти» вже залогіненому.
  if (auth.isRestoring) return null

  if (!auth.user) {
    return (
      <>
        <button type="button" onClick={openSignIn} className="text-sm text-ink-2 hover:text-ink">
          {t('account.signIn')}
        </button>
        <button type="button" onClick={openSignIn} className="btn btn-primary">
          {t('account.sell')}
        </button>
      </>
    )
  }

  return (
    <>
      <button
        type="button"
        onClick={() => void auth.logout()}
        className="text-sm text-ink-2 hover:text-ink"
      >
        {t('account.signOut')}
      </button>
      <Link to="/sell" className="btn btn-primary">
        {t('account.sell')}
      </Link>

      {/* Кружечок з ініціалами — найзвичніший вхід у кабінет. */}
      <Link
        to="/account"
        title={t('account.cabinet', { name: auth.user.displayName })}
        className="font-display grid h-[30px] w-[30px] shrink-0 place-items-center rounded-full bg-surface-3 text-[12.5px] font-bold text-ink-2 hover:bg-accent-soft hover:text-accent"
      >
        {initialsOf(auth.user.displayName)}
      </Link>
    </>
  )
}

/** Чи має людина доступ до адмінки — модератор або адміністратор. */
function isStaff(auth: ReturnType<typeof useAuth>): boolean {
  const roles = auth.user?.roles ?? []

  return roles.includes('Moderator') || roles.includes('Admin')
}

/** «Олена Мороз» → «ОМ». Двох літер вистачає, щоб кружечок не переповнився. */
function initialsOf(displayName: string): string {
  return displayName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => word[0].toUpperCase())
    .join('')
}
