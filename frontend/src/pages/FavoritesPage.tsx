import { useState } from 'react'
import { Link } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { fetchFavorites } from '../api/favorites'
import { useAuth } from '../auth/useAuth'
import { openSignIn } from '../auth/signInPrompt'
import { ListingCard } from '../components/catalog/ListingCard'
import { formatCount } from '../format'
import { useTranslation } from '../i18n/useTranslation'

export function FavoritesPage() {
  const { t, tPlural } = useTranslation()

  const auth = useAuth()
  const [page, setPage] = useState(1)

  const favorites = useQuery({
    queryKey: ['favorites', page],
    queryFn: ({ signal }) => fetchFavorites(page, signal),

    // Поки сесія поновлюється, ми ще не знаємо, хто це, — запит без токена
    // повернув би 401 і показав би «увійдіть» уже залогіненому.
    enabled: !auth.isRestoring && auth.user !== null,
    placeholderData: keepPreviousData,
  })

  if (auth.isRestoring) {
    return <Notice>{t('favorites.loading')}</Notice>
  }

  if (!auth.user) {
    return (
      <Notice>
        {t('favorites.signInLead')}{' '}
        <button type="button" onClick={openSignIn} className="text-accent hover:underline">
          {t('favorites.signIn')}
        </button>
      </Notice>
    )
  }

  const total = favorites.data?.totalCount ?? 0

  return (
    <div className="wrap grid gap-3.5 py-[26px]">
      <div>
        <h1 className="font-display text-[25px] font-bold">{t('favorites.title')}</h1>
        <p className="text-[13px] text-ink-2">
          {favorites.isPending
            ? t('favorites.loading')
            : tPlural('favorites.count', total, { count: formatCount(total) })}
        </p>
      </div>

      {favorites.isError && (
        <p className="card p-6 text-sm text-danger">{t('favorites.failed')}</p>
      )}

      {favorites.data && favorites.data.items.length === 0 && (
        <p className="card p-10 text-center text-sm text-ink-2">
          {t('favorites.emptyLead')}{' '}
          <Link to="/" className="text-accent hover:underline">
            {t('favorites.toCatalog')}
          </Link>
          .
        </p>
      )}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {favorites.data?.items.map((listing) => (
          <ListingCard key={listing.id} listing={listing} />
        ))}
      </div>

      {favorites.data && favorites.data.totalPages > 1 && (
        <nav className="mt-2 flex items-center justify-center gap-1.5">
          <button
            type="button"
            className="btn"
            disabled={!favorites.data.hasPrevious}
            onClick={() => setPage((current) => current - 1)}
            title={t('favorites.previousPage')}
            aria-label={t('favorites.previousPage')}
          >
            ←
          </button>
          <span className="px-3 font-mono text-sm tabular-nums">
            {favorites.data.page} / {favorites.data.totalPages}
          </span>
          <button
            type="button"
            className="btn"
            disabled={!favorites.data.hasNext}
            onClick={() => setPage((current) => current + 1)}
            title={t('favorites.nextPage')}
            aria-label={t('favorites.nextPage')}
          >
            →
          </button>
        </nav>
      )}
    </div>
  )
}

function Notice({ children }: { children: React.ReactNode }) {
  return (
    <div className="wrap py-16">
      <p className="card mx-auto max-w-[520px] p-10 text-center text-sm text-ink-2">{children}</p>
    </div>
  )
}
