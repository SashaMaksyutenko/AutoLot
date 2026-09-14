import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { fetchRecentlyViewed, historyLimit } from '../api/history'
import { useAuth } from '../auth/useAuth'
import { openSignIn } from '../auth/signInPrompt'
import { ListingCard } from '../components/catalog/ListingCard'
import { useTranslation } from '../i18n/useTranslation'
import { usePageMeta } from '../seo/usePageMeta'

/**
 * Уся історія переглядів.
 *
 * Під карткою авто й у кабінеті показуємо чотири — там це підказка збоку.
 * Хто хоче переглянути все, приходить сюди за посиланням із заголовка.
 */
export function ViewedPage() {
  const { t, tPlural } = useTranslation()
  const auth = useAuth()

  usePageMeta(t('viewed.title'))

  const viewed = useQuery({
    queryKey: ['recently-viewed', historyLimit],
    queryFn: ({ signal }) => fetchRecentlyViewed(historyLimit, signal),
    enabled: auth.user !== null,
  })

  if (!auth.user) {
    return (
      <Notice>
        {t('viewed.signInFirst')}{' '}
        <button type="button" onClick={openSignIn} className="text-accent hover:underline">
          {t('account.signIn')}
        </button>
      </Notice>
    )
  }

  if (viewed.isPending) return <Notice>{t('viewed.loading')}</Notice>

  const items = viewed.data ?? []

  return (
    <div className="wrap grid gap-3.5 py-[26px]">
      <div>
        <h1 className="font-display text-[25px] font-bold">{t('viewed.title')}</h1>
        <p className="text-[13px] text-ink-2">
          {items.length === 0
            ? t('viewed.empty')
            : tPlural('viewed.count', items.length, { count: items.length })}
        </p>
      </div>

      {items.length === 0 ? (
        <p className="card p-10 text-center text-sm text-ink-2">
          {t('viewed.emptyLead')}{' '}
          <Link to="/" className="text-accent hover:underline">
            {t('favorites.toCatalog')}
          </Link>
        </p>
      ) : (
        <div className="grid gap-3.5 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {items.map((listing) => (
            <ListingCard key={listing.id} listing={listing} />
          ))}
        </div>
      )}

      {/*
        Межу називаємо вголос: людина має розуміти, чому тут не все, що
        вона колись відкривала, а не гадати, куди поділося.
      */}
      <p className="text-[12.5px] text-ink-3">{t('viewed.limit', { limit: historyLimit })}</p>
    </div>
  )
}

function Notice({ children }: { children: React.ReactNode }) {
  return <div className="wrap py-16 text-center text-sm text-ink-2">{children}</div>
}
