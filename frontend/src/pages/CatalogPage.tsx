import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import {
  emptyFilters,
  searchCatalog,
  type CatalogFilters,
  type CatalogSort,
} from '../api/catalog'
import { FilterRail } from '../components/catalog/FilterRail'
import { ListingCard } from '../components/catalog/ListingCard'
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
    // Дві колонки: вузька смуга фільтрів і видача. На екранах до 1024 px
    // колонка одна — фільтри стають звичайним блоком над списком.
    <div className="wrap grid items-start gap-[22px] py-[26px] lg:grid-cols-[258px_minmax(0,1fr)]">
      <FilterRail
        filters={filters}
        onChange={patchFilters}
        onApply={setFilters}
        onReset={() => setFilters(emptyFilters)}
        totalCount={total}
      />

      <main className="flex min-w-0 flex-col gap-3.5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="font-display text-[25px] font-bold">Легкові автомобілі</h1>
            <p className="text-[13px] text-ink-2">
              {results.isPending ? (
                'Шукаємо…'
              ) : (
                <>
                  Знайдено{' '}
                  <span className="font-mono font-semibold text-ink tabular-nums">
                    {formatCount(total)}
                  </span>{' '}
                  {plural(total, 'оголошення', 'оголошення', 'оголошень')}
                </>
              )}
            </p>
          </div>

          <label className="flex items-center gap-2 text-[13px] text-ink-2">
            <span>Сортувати</span>
            <select
              value={filters.sort}
              onChange={(event) => patchFilters({ sort: event.target.value as CatalogSort })}
              className="control w-auto"
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
          <p className="card p-6 text-sm text-danger">
            Не вдалося отримати каталог. Перевірте, що AutoLot.Api запущено на порту 5080.
          </p>
        )}

        {results.data && results.data.items.length === 0 && (
          <p className="card p-10 text-center text-sm text-ink-2">
            За такими фільтрами нічого немає. Спробуйте прибрати частину умов.
          </p>
        )}

        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {results.data?.items.map((listing) => (
            <ListingCard key={listing.id} listing={listing} />
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
    <nav className="mt-2 flex items-center justify-center gap-1.5">
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
      className={`btn min-w-[36px] px-2.5 tabular-nums ${active ? 'btn-primary' : ''}`}
    >
      {label}
    </button>
  )
}
