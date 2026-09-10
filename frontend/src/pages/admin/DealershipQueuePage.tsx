import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError } from '../../api/client'
import { fetchDealerships, setVerification, type DealershipCard } from '../../api/dealership'
import { useTranslation } from '../../i18n/useTranslation'

/**
 * Верифікація салонів.
 *
 * Бейдж перевіреного впливає на довіру до цін, тож ставить його майданчик, а
 * не сам салон. Список показуємо повністю — і неперевірені, і перевірені:
 * зняти бейдж потрібно так само, як і поставити, а окремий екран заради
 * зняття був би зайвим.
 */
export function DealershipQueuePage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const [onlyPending, setOnlyPending] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const dealerships = useQuery({
    queryKey: ['admin-dealerships'],
    queryFn: ({ signal }) => fetchDealerships({}, signal),
  })

  const decide = useMutation({
    mutationFn: ({ id, verified }: { id: number; verified: boolean }) =>
      setVerification(id, verified),
    onSuccess: () => {
      setError(null)
      void queryClient.invalidateQueries({ queryKey: ['admin-dealerships'] })

      // Бейдж видно й покупцям — у видачі та на вітрині, тож їхні списки
      // теж застаріли.
      void queryClient.invalidateQueries({ queryKey: ['dealerships'] })
    },
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : t('error.network')),
  })

  const all = dealerships.data ?? []
  const shown = onlyPending ? all.filter((item) => !item.isVerified) : all
  const pending = all.filter((item) => !item.isVerified).length

  return (
    <div className="grid gap-3.5">
      <div>
        <h1 className="font-display text-[25px] font-bold">{t('verify.title')}</h1>
        <p className="text-[13px] text-ink-2">
          {dealerships.isPending ? t('admin.loading') : t('verify.pending', { count: pending })}
        </p>
      </div>

      <label className="flex cursor-pointer items-center gap-2 text-[13.5px]">
        <input
          type="checkbox"
          checked={onlyPending}
          onChange={(event) => setOnlyPending(event.target.checked)}
          className="h-[15px] w-[15px] accent-accent"
        />
        <span>{t('verify.onlyPending')}</span>
      </label>

      {error && (
        <p className="rounded-control bg-danger-soft px-3 py-2 text-[13px] text-danger">{error}</p>
      )}

      {!dealerships.isPending && shown.length === 0 && (
        <p className="card p-10 text-center text-sm text-ink-2">{t('verify.empty')}</p>
      )}

      {shown.map((dealership) => (
        <Row
          key={dealership.id}
          dealership={dealership}
          busy={decide.isPending}
          onDecide={(verified) => decide.mutate({ id: dealership.id, verified })}
        />
      ))}
    </div>
  )
}

function Row({
  dealership,
  busy,
  onDecide,
}: {
  dealership: DealershipCard
  busy: boolean
  onDecide: (verified: boolean) => void
}) {
  const { t } = useTranslation()

  return (
    <article className="card flex flex-wrap items-center gap-x-3 gap-y-2 p-3">
      <div className="min-w-0 flex-1">
        <Link
          to={`/dealers/${dealership.slug}`}
          className="text-[15px] font-semibold hover:text-accent"
        >
          {dealership.name}
        </Link>

        <div className="text-[12.5px] text-ink-3">
          {dealership.cityName} · {t('verify.listings', { count: dealership.activeListingCount })}
        </div>
      </div>

      <span className={`pill ${dealership.isVerified ? 'pill-good' : ''}`}>
        {dealership.isVerified ? t('salon.verified') : t('salon.awaitingVerification')}
      </span>

      <button
        type="button"
        onClick={() => onDecide(!dealership.isVerified)}
        disabled={busy}
        className={dealership.isVerified ? 'btn' : 'btn btn-primary'}
      >
        {dealership.isVerified ? t('verify.revoke') : t('verify.grant')}
      </button>
    </article>
  )
}
