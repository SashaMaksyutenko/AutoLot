import { useQuery } from '@tanstack/react-query'
import { fetchStats } from '../../api/admin'
import { useAuth } from '../../auth/useAuth'
import { formatCount } from '../../format'
import { useTranslation } from '../../i18n/useTranslation'

/**
 * Головна адмінки. Свідомо кілька чисел, а не звіт: вона має відповідати на
 * питання «чи все гаразд», а не заміняти аналітику.
 */
export function AdminOverviewPage() {
  const { t } = useTranslation()

  const auth = useAuth()
  const isAdmin = auth.user?.roles.includes('Admin') ?? false

  const stats = useQuery({
    queryKey: ['admin-stats'],
    queryFn: ({ signal }) => fetchStats(signal),

    // Показники — привілей адміністратора; модератор побачив би 403.
    enabled: isAdmin,
  })

  if (!isAdmin) {
    return (
      <section className="card p-6 text-sm text-ink-2">
        {t('overview.moderatorOnly')}
      </section>
    )
  }

  if (stats.isPending) {
    return <section className="card p-6 text-sm text-ink-2">{t('admin.loading')}</section>
  }

  if (stats.isError || !stats.data) {
    return <section className="card p-6 text-sm text-danger">{t('overview.failed')}</section>
  }

  const data = stats.data

  return (
    <>
      <h1 className="font-display text-[25px] font-bold">{t('overview.title')}</h1>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        <Tile
          label={t('overview.awaitingModeration')}
          value={data.pendingModeration}
          hint={t('overview.awaitingHint')}
          urgent={data.pendingModeration > 0}
        />
        <Tile
          label={t('overview.openReports')}
          value={data.pendingReports}
          hint={t('overview.openReportsHint')}
          urgent={data.pendingReports > 0}
        />
        <Tile label={t('overview.activeListings')} value={data.activeListings} />
        <Tile label={t('overview.activeAuctions')} value={data.activeAuctions} />
        <Tile label={t('overview.totalUsers')} value={data.totalUsers} />
        <Tile
          label={t('overview.banned')}
          value={data.bannedUsers}
          urgent={data.bannedUsers > 0}
        />
        <Tile
          label={t('overview.dealerships')}
          value={data.dealerships}
          hint={
            data.unverifiedDealerships > 0
              ? t('overview.unverified', { count: data.unverifiedDealerships })
              : t('overview.allVerified')
          }
          urgent={data.unverifiedDealerships > 0}
        />
      </div>
    </>
  )
}

function Tile({
  label,
  value,
  hint,
  urgent,
}: {
  label: string
  value: number
  hint?: string
  urgent?: boolean
}) {
  return (
    <article
      className={`card grid gap-1 border-l-[3px] p-4 ${urgent ? 'border-l-signal' : 'border-l-accent'}`}
    >
      <span className="eyebrow">{label}</span>
      <span className="font-display text-[25px] font-bold tabular-nums">{formatCount(value)}</span>
      {hint && <span className="text-[12px] text-ink-3">{hint}</span>}
    </article>
  )
}
