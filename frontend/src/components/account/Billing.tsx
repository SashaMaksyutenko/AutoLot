import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  fetchPlans,
  fetchSubscription,
  fetchWallet,
  subscribe,
  topUpWallet,
  type PlanCard,
  type WalletEntry,
} from '../../api/billing'
import { ApiError } from '../../api/client'
import { formatDateTime } from '../../format'
import { useTranslation } from '../../i18n/useTranslation'
import type { MessageKey } from '../../i18n/messages'

/**
 * Гаманець і тарифи в кабінеті.
 *
 * Показані поруч навмисно: тариф оплачується з балансу, і людина має бачити
 * обидві цифри одночасно. Розвівши їх по різних сторінках, ми змусили б
 * ходити туди-сюди, щоб зрозуміти, чому кнопка «оформити» не спрацювала.
 */
export function Billing() {
  const { t } = useTranslation()

  const queryClient = useQueryClient()

  const wallet = useQuery({
    queryKey: ['wallet'],
    queryFn: ({ signal }) => fetchWallet(signal),
  })

  const subscription = useQuery({
    queryKey: ['subscription'],
    queryFn: ({ signal }) => fetchSubscription(signal),
  })

  const plans = useQuery({
    queryKey: ['plans'],
    queryFn: ({ signal }) => fetchPlans(signal),
  })

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['wallet'] })
    void queryClient.invalidateQueries({ queryKey: ['subscription'] })
    void queryClient.invalidateQueries({ queryKey: ['plans'] })
  }

  if (wallet.isPending || subscription.isPending) {
    return <section className="card p-6 text-sm text-ink-2">{t('billing.loading')}</section>
  }

  if (wallet.isError || subscription.isError || !wallet.data || !subscription.data) {
    return <section className="card p-6 text-sm text-danger">{t('billing.failed')}</section>
  }

  const state = subscription.data
  const limit = state.plan.listingLimit

  return (
    <section className="grid gap-3">
      <h2 className="font-display text-[19px] font-bold">{t('billing.title')}</h2>

      <div className="card grid gap-3 p-4">
        <div className="flex flex-wrap items-baseline justify-between gap-3">
          <div>
            <span className="eyebrow">{t('billing.yourPlan')}</span>
            <div className="font-display text-[21px] font-bold">{state.plan.name}</div>
          </div>

          <div className="text-right">
            <span className="eyebrow">{t('billing.balance')}</span>
            <div className="font-display text-[21px] font-bold tabular-nums">
              {wallet.data.balance.toFixed(2)}
            </div>
          </div>
        </div>

        <p className="text-[13px] text-ink-2">
          {limit === null
            ? t('billing.unlimited', { active: state.activeListings })
            : t('billing.usedOf', { active: state.activeListings, limit })}
          {state.activeUntil &&
            t('billing.paidUntil', { date: formatDateTime(state.activeUntil) })}
        </p>

        <TopUpForm onDone={refresh} />
      </div>

      <div className="grid gap-3 sm:grid-cols-3">
        {plans.data?.map((plan) => (
          <PlanTile key={plan.id} plan={plan} balance={wallet.data.balance} onDone={refresh} />
        ))}
      </div>

      {wallet.data.recent.length > 0 && <History entries={wallet.data.recent} />}
    </section>
  )
}

/**
 * Поповнення. Кнопки з готовими сумами, а не порожнє поле: у демо точна
 * сума нікого не цікавить, а вводити її вручну — зайва робота.
 */
function TopUpForm({ onDone }: { onDone: () => void }) {
  const { t } = useTranslation()

  const [error, setError] = useState<string | null>(null)

  const top = useMutation({
    mutationFn: (amount: number) => topUpWallet(amount),
    onSuccess: onDone,
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : t('billing.topUpFailed')),
  })

  return (
    <div className="grid gap-2 border-t border-line pt-3">
      <span className="text-[11.5px] font-semibold text-ink-2">
        {t('billing.topUpNote')}
      </span>

      <div className="flex flex-wrap gap-2">
        {[100, 500, 1000].map((amount) => (
          <button
            key={amount}
            type="button"
            onClick={() => {
              setError(null)
              top.mutate(amount)
            }}
            disabled={top.isPending}
            className="btn"
          >
            +{amount}
          </button>
        ))}
      </div>

      {error && <p className="text-[12px] text-danger">{error}</p>}
    </div>
  )
}

function PlanTile({
  plan,
  balance,
  onDone,
}: {
  plan: PlanCard
  balance: number
  onDone: () => void
}) {
  const { t } = useTranslation()

  const [error, setError] = useState<string | null>(null)

  const buy = useMutation({
    mutationFn: () => subscribe(plan.code),
    onSuccess: onDone,
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : t('billing.buyFailed')),
  })

  const affordable = balance >= plan.price

  return (
    <article
      className={`card grid gap-2 border-l-[3px] p-4 ${
        plan.isCurrent ? 'border-l-accent' : 'border-l-transparent'
      }`}
    >
      <div className="flex items-center gap-2">
        <span className="font-display text-[17px] font-semibold">{plan.name}</span>
        {plan.isCurrent && <span className="pill pill-accent">{t('billing.current')}</span>}
      </div>

      <div className="font-mono text-[15px] tabular-nums">
        {plan.price === 0
          ? t('billing.free')
          : t('billing.pricePerDays', { price: plan.price, days: plan.durationDays })}
      </div>

      <p className="text-[12.5px] text-ink-2">{plan.description}</p>

      {error && <p className="text-[12px] text-danger">{error}</p>}

      {/* Базовий тариф не оформлюють — він діє й так. */}
      {!plan.isDefault && (
        <button
          type="button"
          onClick={() => {
            setError(null)
            buy.mutate()
          }}
          disabled={buy.isPending || !affordable}
          className="btn btn-primary justify-self-start"
          title={affordable ? undefined : t('billing.topUpFirst')}
        >
          {buy.isPending
            ? t('billing.processing')
            : plan.isCurrent
              ? t('billing.extend')
              : t('billing.subscribe')}
        </button>
      )}
    </article>
  )
}

function History({ entries }: { entries: WalletEntry[] }) {
  const { t } = useTranslation()

  const labels: Record<string, MessageKey> = {
    TopUp: 'wallet.TopUp',
    SubscriptionCharge: 'wallet.SubscriptionCharge',
    Refund: 'wallet.Refund',
  }

  return (
    <div className="card grid gap-1 p-4">
      <span className="eyebrow pb-1">{t('billing.movement')}</span>

      {entries.map((entry) => (
        <div key={entry.id} className="flex items-center gap-3 py-1 text-[13px]">
          <span className="flex-1 truncate">
            {labels[entry.kind] ? t(labels[entry.kind]) : entry.kind}
          </span>

          <span className="text-[11.5px] text-ink-3">{formatDateTime(entry.createdAt)}</span>

          {/* Списання позначаємо кольором, а не лише мінусом: мінус легко
              не помітити в стовпчику однакових чисел. */}
          <span
            className={`w-[90px] text-right font-mono tabular-nums ${
              entry.amount < 0 ? 'text-danger' : 'text-accent'
            }`}
          >
            {entry.amount > 0 ? '+' : ''}
            {entry.amount.toFixed(2)}
          </span>

          <span className="w-[90px] text-right font-mono text-[12px] text-ink-3 tabular-nums">
            {entry.balanceAfter.toFixed(2)}
          </span>
        </div>
      ))}
    </div>
  )
}
