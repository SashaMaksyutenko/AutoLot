import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { emptyFilters, searchCatalog, type CatalogSort } from '../api/catalog'
import { fetchDealership } from '../api/dealership'
import { ListingCard } from '../components/catalog/ListingCard'
import { VerifiedMark } from '../components/catalog/ListingCard'
import { formatCount } from '../format'
import { useTranslation } from '../i18n/useTranslation'
import type { MessageKey } from '../i18n/messages'

const sortKeys: Record<CatalogSort, MessageKey> = {
  Newest: 'sort.Newest',
  PriceAscending: 'sort.PriceAscending',
  PriceDescending: 'sort.PriceDescending',
  MileageAscending: 'sort.MileageAscending',
  YearDescending: 'sort.YearDescending',
}

/**
 * Вітрина салону: шапка з назвою й бейджем, під нею — всі його оголошення.
 *
 * Видача тут — той самий каталог із фільтром за салоном, а не окремий
 * механізм. Так вітрина безкоштовно отримує сортування, пагінацію й ті самі
 * картки, а зміни в каталозі не доводиться дублювати.
 */
export function DealershipPage() {
  const { t, tPlural } = useTranslation()

  const { slug } = useParams()
  const [page, setPage] = useState(1)
  const [sort, setSort] = useState<CatalogSort>('Newest')

  const dealership = useQuery({
    queryKey: ['dealership', slug],
    queryFn: ({ signal }) => fetchDealership(slug!, signal),
    enabled: slug !== undefined,
  })

  const listings = useQuery({
    queryKey: ['dealership-listings', dealership.data?.id, page, sort],
    queryFn: ({ signal }) =>
      searchCatalog({ ...emptyFilters, dealershipId: dealership.data!.id, page, sort }, signal),
    enabled: dealership.data !== undefined,
    placeholderData: keepPreviousData,
  })

  if (dealership.isPending) {
    return <Notice>{t('dealer.loading')}</Notice>
  }

  if (dealership.isError || !dealership.data) {
    return (
      <Notice>
        {t('dealer.notFound')}{' '}
        <Link to="/dealers" className="text-accent hover:underline">
          {t('dealer.allDealers')}
        </Link>
      </Notice>
    )
  }

  const salon = dealership.data

  return (
    <div className="wrap grid gap-4 py-[26px]">
      <nav className="text-[13px] text-ink-2">
        <Link to="/dealers" className="hover:text-accent hover:underline">
          {t('dealer.crumb')}
        </Link>
        <span className="px-1.5 text-ink-3">/</span>
        <span>{salon.name}</span>
      </nav>

      <header className="card grid gap-3 p-5">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0">
            <h1 className="font-display flex items-center gap-2 text-[25px] font-bold">
              {salon.isVerified && <VerifiedMark size={20} />}
              {salon.name}
            </h1>
            <p className="text-[13px] text-ink-2">{salon.cityName}</p>
          </div>

          <span className={`pill ${salon.isVerified ? 'pill-good' : ''}`}>
            {salon.isVerified ? t('dealer.verified') : t('dealer.unverified')}
          </span>
        </div>

        {salon.description && (
          <p className="text-sm leading-relaxed whitespace-pre-line text-ink-2">
            {salon.description}
          </p>
        )}

        <div className="border-t border-line pt-3 text-[13px] text-ink-2">
          <span className="font-mono font-semibold text-ink tabular-nums">
            {formatCount(salon.activeListingCount)}
          </span>{' '}
          {tPlural('dealer.onSale', salon.activeListingCount, {
            count: formatCount(salon.activeListingCount),
          })}
        </div>
      </header>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="font-display text-[19px] font-bold">{t('dealer.listings')}</h2>

        <label className="flex items-center gap-2 text-[13px] text-ink-2">
          <span>{t('dealer.sort')}</span>
          <select
            value={sort}
            onChange={(event) => {
              setSort(event.target.value as CatalogSort)
              setPage(1)
            }}
            className="control w-auto"
          >
            {Object.entries(sortKeys).map(([value, key]) => (
              <option key={value} value={value}>
                {t(key)}
              </option>
            ))}
          </select>
        </label>
      </div>

      {listings.data?.items.length === 0 && (
        <p className="card p-10 text-center text-sm text-ink-2">
          {t('dealer.noListings')}
        </p>
      )}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {listings.data?.items.map((listing) => (
          <ListingCard key={listing.id} listing={listing} />
        ))}
      </div>

      {listings.data && listings.data.totalPages > 1 && (
        <nav className="mt-2 flex items-center justify-center gap-1.5">
          <button
            type="button"
            className="btn"
            disabled={!listings.data.hasPrevious}
            onClick={() => setPage((current) => current - 1)}
            title={t('dealer.previousPage')}
            aria-label={t('dealer.previousPage')}
          >
            ←
          </button>
          <span className="px-3 font-mono text-sm tabular-nums">
            {listings.data.page} / {listings.data.totalPages}
          </span>
          <button
            type="button"
            className="btn"
            disabled={!listings.data.hasNext}
            onClick={() => setPage((current) => current + 1)}
            title={t('dealer.nextPage')}
            aria-label={t('dealer.nextPage')}
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
