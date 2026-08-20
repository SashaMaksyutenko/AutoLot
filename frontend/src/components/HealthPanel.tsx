import { useQuery } from '@tanstack/react-query'
import { fetchHealth, type HealthStatus } from '../api/health'

const statusStyles: Record<HealthStatus, string> = {
  Healthy: 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-300',
  Degraded: 'bg-amber-500/15 text-amber-700 dark:text-amber-300',
  Unhealthy: 'bg-rose-500/15 text-rose-700 dark:text-rose-300',
}

function StatusBadge({ status }: { status: HealthStatus }) {
  return (
    <span
      className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${statusStyles[status]}`}
    >
      {status}
    </span>
  )
}

export function HealthPanel() {
  const { data, error, isPending, refetch, isFetching } = useQuery({
    queryKey: ['health'],
    queryFn: ({ signal }) => fetchHealth(signal),
    refetchInterval: 15_000,
    retry: false,
  })

  return (
    <section className="rounded-xl border border-neutral-200 bg-white p-5 dark:border-neutral-800 dark:bg-neutral-900">
      <header className="mb-4 flex items-center justify-between gap-4">
        <h2 className="text-sm font-semibold tracking-wide text-neutral-500 uppercase dark:text-neutral-400">
          Стан бекенду
        </h2>
        <button
          type="button"
          onClick={() => void refetch()}
          disabled={isFetching}
          className="rounded-md border border-neutral-300 px-3 py-1 text-xs font-medium text-neutral-700 transition hover:bg-neutral-100 disabled:opacity-50 dark:border-neutral-700 dark:text-neutral-200 dark:hover:bg-neutral-800"
        >
          {isFetching ? 'Перевіряю…' : 'Оновити'}
        </button>
      </header>

      {isPending && <p className="text-sm text-neutral-500">Запитую /health…</p>}

      {error && (
        <p className="text-sm text-rose-600 dark:text-rose-400">
          API недоступний. Переконайтеся, що AutoLot.Api запущено на порту 5080.
        </p>
      )}

      {data && (
        <div className="space-y-3">
          <div className="flex items-center gap-3">
            <StatusBadge status={data.status} />
            <span className="text-xs text-neutral-500">
              {data.totalDurationMs} мс
            </span>
          </div>

          <ul className="divide-y divide-neutral-200 dark:divide-neutral-800">
            {data.checks.map((check) => (
              <li
                key={check.name}
                className="flex items-center justify-between gap-4 py-2"
              >
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium">{check.name}</p>
                  {check.error && (
                    <p className="truncate text-xs text-rose-600 dark:text-rose-400">
                      {check.error}
                    </p>
                  )}
                </div>
                <StatusBadge status={check.status} />
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  )
}
