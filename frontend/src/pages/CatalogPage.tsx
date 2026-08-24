import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import {
  emptyFilters,
  searchCatalog,
  type CatalogFilters,
  type CatalogSort,
} from '../api/catalog'
import { FilterRail } from '../components/catalog/FilterRail'
import { ListingRow } from '../components/catalog/ListingRow'
import { formatCount, plural } from '../format'

const sortLabels: Record<CatalogSort, string> = {
  Newest: 'Найновіші',
  PriceAscending: 'Спочатку дешевші',
  PriceDescending: 'Спочатку дорожчі',
  MileageAscending: 'Менший пробіг',
  YearDescending: 'Свіжіший рік',
}

export function CatalogPage() {
  const [filters, setFilters] = useState<CatalogFilters>(emptyFilters)

  const results = useQuery({
    queryKey: ['catalog', filters],
    queryFn: ({ signal }) => searchCatalog(filters, signal),

    // Стара сторінка лишається на екрані, поки вантажиться нова: без цього
    // список блимав би порожнечею на кожну зміну фільтра.
    placeholderData: keepPreviousData,
  })

  /** Будь-яка зміна фільтра повертає на першу сторінку — інакше можна опинитися на сьомій сторінці з трьох. */
  function patchFilters(patch: Partial<CatalogFilters>) {
    setFilters((current) => ({ ...current, ...patch, page: patch.page ?? 1 }))
  }

  const total = results.data?.totalCount ?? 0

  return (
    <div className="min-h-dvh">
      <Header />

      <div className="flex gap-6 px-10 pt-6 pb-12">
        <FilterRail
          filters={filters}
          onChange={patchFilters}
          onReset={() => setFilters(emptyFilters)}
          totalCount={total}
        />

        <main className="flex min-w-0 flex-grow flex-col gap-3">
          <div className="flex items-end justify-between gap-6">
            <div>
              <h1 className="text-[22px] font-semibold tracking-tight">Легкові автомобілі</h1>
              <p className="mt-0.5 text-[13px] text-subtle">
                {results.isPending ? (
                  'Шукаємо…'
                ) : (
                  <>
                    Знайдено{' '}
                    <span className="font-mono font-semibold text-ink">{formatCount(total)}</span>{' '}
                    {plural(total, 'оголошення', 'оголошення', 'оголошень')}
                  </>
                )}
              </p>
            </div>

            <label className="flex items-center gap-2 text-[13px] text-subtle">
              <span>Сортувати</span>
              <select
                value={filters.sort}
                onChange={(event) => patchFilters({ sort: event.target.value as CatalogSort })}
                className="h-[34px] rounded-sm border border-line-strong bg-surface px-2.5 text-[13px] font-medium text-ink"
              >
                {Object.entries(sortLabels).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
            </label>
          </div>

          {results.isError && (
            <p className="rounded-md border border-line bg-surface p-6 text-sm text-[#b3261e]">
              Не вдалося отримати каталог. Перевірте, що AutoLot.Api запущено на порту 5080.
            </p>
          )}

          {results.data && results.data.items.length === 0 && (
            <p className="rounded-md border border-line bg-surface p-10 text-center text-sm text-subtle">
              За такими фільтрами нічого немає. Спробуйте прибрати частину умов.
            </p>
          )}

          <div className="flex flex-col gap-2">
            {results.data?.items.map((listing) => (
              <ListingRow key={listing.id} listing={listing} />
            ))}
          </div>

          {results.data && results.data.totalPages > 1 && (
            <Pagination
              page={results.data.page}
              totalPages={results.data.totalPages}
              onPage={(page) => patchFilters({ page })}
            />
          )}
        </main>
      </div>
    </div>
  )
}

function Header() {
  return (
    <header className="flex h-15 items-center gap-8 border-b border-line bg-surface px-10">
      <div className="text-[19px] font-bold tracking-tight">
        Auto<span className="text-brand">Lot</span>
      </div>
      <nav className="flex gap-6 text-sm font-medium text-muted">
        <span className="text-ink">Каталог</span>
        <span>Аукціони</span>
        <span>Дилери</span>
      </nav>
      <div className="flex-grow" />
      <div className="flex items-center gap-4 text-[13px] text-muted">
        <span>Увійти</span>
        <span className="rounded-sm bg-brand px-4 py-2 text-[13px] font-semibold text-white">
          Подати оголошення
        </span>
      </div>
    </header>
  )
}

function Pagination({
  page,
  totalPages,
  onPage,
}: {
  page: number
  totalPages: number
  onPage: (page: number) => void
}) {
  // Показуємо вікно навколо поточної сторінки: за десяти сторінок список
  // номерів ще влазить, за ста — вже ні.
  const from = Math.max(1, Math.min(page - 2, totalPages - 4))
  const pages = Array.from({ length: Math.min(5, totalPages) }, (_, index) => from + index)

  return (
    <nav className="mt-2 flex items-center justify-center gap-1 font-mono text-sm">
      <PageButton label="←" disabled={page === 1} onClick={() => onPage(page - 1)} />
      {pages.map((value) => (
        <PageButton
          key={value}
          label={String(value)}
          active={value === page}
          onClick={() => onPage(value)}
        />
      ))}
      <PageButton label="→" disabled={page === totalPages} onClick={() => onPage(page + 1)} />
    </nav>
  )
}

function PageButton({
  label,
  active,
  disabled,
  onClick,
}: {
  label: string
  active?: boolean
  disabled?: boolean
  onClick: () => void
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className={`h-[34px] min-w-[34px] rounded-sm px-2 ${
        active
          ? 'bg-brand font-semibold text-white'
          : 'border border-line bg-surface text-muted disabled:opacity-40'
      }`}
    >
      {label}
    </button>
  )
}
