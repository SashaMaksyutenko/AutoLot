import { useQuery } from '@tanstack/react-query'
import { fetchStats } from '../../api/admin'
import { useAuth } from '../../auth/useAuth'
import { formatCount } from '../../format'

/**
 * Головна адмінки. Свідомо кілька чисел, а не звіт: вона має відповідати на
 * питання «чи все гаразд», а не заміняти аналітику.
 */
export function AdminOverviewPage() {
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
        Показники майданчика доступні адміністраторам. Ваш розділ — черга модерації.
      </section>
    )
  }

  if (stats.isPending) {
    return <section className="card p-6 text-sm text-ink-2">Завантажуємо…</section>
  }

  if (stats.isError || !stats.data) {
    return <section className="card p-6 text-sm text-danger">Не вдалося отримати показники.</section>
  }

  const data = stats.data

  return (
    <>
      <h1 className="font-display text-[25px] font-bold">Огляд майданчика</h1>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        <Tile
          label="Чекають модерації"
          value={data.pendingModeration}
          hint="Що більше, то довша черга"
          urgent={data.pendingModeration > 0}
        />
        <Tile
          label="Скарги на розгляді"
          value={data.pendingReports}
          hint="Опубліковане, на що поскаржилися"
          urgent={data.pendingReports > 0}
        />
        <Tile label="Активні оголошення" value={data.activeListings} />
        <Tile label="Активні торги" value={data.activeAuctions} />
        <Tile label="Користувачі" value={data.totalUsers} />
        <Tile
          label="Заблоковані"
          value={data.bannedUsers}
          urgent={data.bannedUsers > 0}
        />
        <Tile
          label="Салони"
          value={data.dealerships}
          hint={
            data.unverifiedDealerships > 0
              ? `${data.unverifiedDealerships} без перевірки`
              : 'Усі перевірені'
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
