import { useState } from 'react'
import { Link } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { fetchFavorites } from '../api/favorites'
import { useAuth } from '../auth/useAuth'
import { openSignIn } from '../auth/signInPrompt'
import { ListingCard } from '../components/catalog/ListingCard'
import { formatCount, plural } from '../format'

export function FavoritesPage() {
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
    return <Notice>Завантажуємо…</Notice>
  }

  if (!auth.user) {
    return (
      <Notice>
        Обране прив'язане до акаунта, а не до браузера — так воно лишається з
        вами на будь-якому пристрої.{' '}
        <button type="button" onClick={openSignIn} className="text-accent hover:underline">
          Увійти
        </button>
      </Notice>
    )
  }

  const total = favorites.data?.totalCount ?? 0

  return (
    <div className="wrap grid gap-3.5 py-[26px]">
      <div>
        <h1 className="font-display text-[25px] font-bold">Обране</h1>
        <p className="text-[13px] text-ink-2">
          {favorites.isPending ? (
            'Завантажуємо…'
          ) : (
            <>
              <span className="font-mono font-semibold text-ink tabular-nums">
                {formatCount(total)}
              </span>{' '}
              {plural(total, 'оголошення', 'оголошення', 'оголошень')}
            </>
          )}
        </p>
      </div>

      {favorites.isError && (
        <p className="card p-6 text-sm text-danger">Не вдалося отримати обране.</p>
      )}

      {favorites.data && favorites.data.items.length === 0 && (
        <p className="card p-10 text-center text-sm text-ink-2">
          Тут поки порожньо. Натисніть сердечко на будь-якому оголошенні —{' '}
          <Link to="/" className="text-accent hover:underline">
            перейти до каталогу
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
