import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  deleteSearch,
  fetchSavedSearches,
  saveSearch,
  setSearchNotifications,
  type SavedSearchCard,
} from '../../api/savedSearches'
import { ApiError } from '../../api/client'
import { emptyFilters, type CatalogFilters } from '../../api/catalog'
import { useAuth } from '../../auth/useAuth'
import { openSignIn } from '../../auth/signInPrompt'
import { plural } from '../../format'

/**
 * Збережені пошуки в смузі фільтрів.
 *
 * Місце обране не випадково: зберігають і відновлюють пошук саме тоді, коли
 * фільтри перед очима. У кабінеті цей список був би за три кліки від того
 * місця, де він потрібен.
 */
export function SavedSearches({
  filters,
  onApply,
}: {
  filters: CatalogFilters
  onApply: (filters: CatalogFilters) => void
}) {
  const auth = useAuth()
  const queryClient = useQueryClient()
  const [naming, setNaming] = useState(false)

  const searches = useQuery({
    queryKey: ['saved-searches'],
    queryFn: ({ signal }) => fetchSavedSearches(signal),
    enabled: !auth.isRestoring && auth.user !== null,
  })

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['saved-searches'] })

  // Гостю показуємо саму можливість, а не ховаємо її: інакше він не
  // дізнається, що вона є.
  if (!auth.user) {
    return (
      <Frame>
        <button
          type="button"
          onClick={openSignIn}
          className="text-left text-[12.5px] text-ink-2 hover:text-accent"
        >
          Увійдіть, щоб зберігати пошуки
        </button>
      </Frame>
    )
  }

  const saved = searches.data ?? []

  return (
    <Frame>
      {saved.map((search) => (
        <SearchRow
          key={search.id}
          search={search}
          onApply={() => onApply({ ...emptyFilters, ...search.query, page: 1 })}
          onDeleted={refresh}
        />
      ))}

      {naming ? (
        <NameForm
          filters={filters}
          onDone={() => {
            setNaming(false)
            void refresh()
          }}
          onCancel={() => setNaming(false)}
        />
      ) : (
        <button
          type="button"
          onClick={() => setNaming(true)}
          className="text-left text-[12.5px] text-accent hover:underline"
        >
          + Зберегти поточний пошук
        </button>
      )}
    </Frame>
  )
}

function Frame({ children }: { children: React.ReactNode }) {
  return (
    <section className="grid gap-1.5 border-b border-line pb-3">
      <span className="eyebrow">Збережені пошуки</span>
      {children}
    </section>
  )
}

function SearchRow({
  search,
  onApply,
  onDeleted,
}: {
  search: SavedSearchCard
  onApply: () => void
  onDeleted: () => void
}) {
  const remove = useMutation({
    mutationFn: () => deleteSearch(search.id),
    onSuccess: onDeleted,
  })

  const notify = useMutation({
    mutationFn: () => setSearchNotifications(search.id, !search.notifyByEmail),
    onSuccess: onDeleted,
  })

  return (
    <div className="flex items-center gap-1.5">
      <button
        type="button"
        onClick={onApply}
        className="flex min-w-0 flex-1 items-baseline gap-1.5 text-left hover:text-accent"
      >
        <span className="truncate text-[13px]">{search.name}</span>

        {/* Число робить назву осмисленою: «Дизельні універсали» — це нуль
            знахідок чи сорок? */}
        <span className="shrink-0 font-mono text-[11.5px] text-ink-3 tabular-nums">
          {search.matchCount}
        </span>
      </button>

      {/*
        Дзвіночок, а не позначка: перемикач у рядку зі списком читався б як
        «обрати цей пошук», а не «слати листи».
      */}
      <button
        type="button"
        onClick={() => notify.mutate()}
        disabled={notify.isPending}
        title={
          search.notifyByEmail
            ? 'Листи про нові збіги увімкнено'
            : 'Повідомляти листом про нові збіги'
        }
        aria-pressed={search.notifyByEmail}
        className={`shrink-0 px-0.5 ${
          search.notifyByEmail ? 'text-accent' : 'text-ink-3 hover:text-ink-2'
        }`}
      >
        <BellIcon on={search.notifyByEmail} />
      </button>

      <button
        type="button"
        onClick={() => remove.mutate()}
        disabled={remove.isPending}
        title="Видалити"
        className="shrink-0 px-1 text-[13px] text-ink-3 hover:text-danger"
      >
        ×
      </button>
    </div>
  )
}

/**
 * Дзвіночок. Увімкнений — залитий, вимкнений — лише контур: так вони
 * розрізняються й на чорно-білому екрані, і для тих, хто не бачить кольору.
 */
function BellIcon({ on }: { on: boolean }) {
  return (
    <svg
      viewBox="0 0 16 16"
      width="13"
      height="13"
      aria-hidden="true"
      fill={on ? 'currentColor' : 'none'}
      stroke="currentColor"
      strokeWidth="1.4"
      strokeLinejoin="round"
    >
      <path d="M8 1.8a3.6 3.6 0 0 0-3.6 3.6c0 3-1.1 4-1.1 4h9.4s-1.1-1-1.1-4A3.6 3.6 0 0 0 8 1.8Z" />
      <path d="M6.6 11.8a1.6 1.6 0 0 0 2.8 0" fill="none" />
    </svg>
  )
}

function NameForm({
  filters,
  onDone,
  onCancel,
}: {
  filters: CatalogFilters
  onDone: () => void
  onCancel: () => void
}) {
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)

  const save = useMutation({
    mutationFn: () => saveSearch(name.trim(), filters),
    onSuccess: onDone,
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : 'Не вдалося зберегти.'),
  })

  return (
    <form
      className="grid gap-1.5"
      onSubmit={(event) => {
        event.preventDefault()
        setError(null)
        save.mutate()
      }}
    >
      <input
        value={name}
        onChange={(event) => setName(event.target.value)}
        maxLength={60}
        autoFocus
        placeholder="Наприклад: дизель до 8000"
        className="control text-[13px]"
      />

      <p className="text-[11.5px] text-ink-3">
        Збережуться саме фільтри — {activeFilterCount(filters)}{' '}
        {plural(activeFilterCount(filters), 'умова', 'умови', 'умов')}. Знайдене
        рахуватиметься щоразу заново, а дзвіночок у списку вмикає листи про
        нові збіги.
      </p>

      {error && <p className="text-[12px] text-danger">{error}</p>}

      <div className="flex gap-1.5">
        <button
          type="submit"
          disabled={save.isPending || name.trim().length === 0}
          className="btn btn-primary"
        >
          {save.isPending ? 'Зберігаємо…' : 'Зберегти'}
        </button>
        <button type="button" onClick={onCancel} className="btn">
          Скасувати
        </button>
      </div>
    </form>
  )
}

/**
 * Скільки фільтрів справді задано. Сортування, сторінка й валюта цін не
 * рахуються — вони є завжди й нічого не звужують.
 */
function activeFilterCount(filters: CatalogFilters): number {
  const ignored = new Set(['sort', 'page', 'priceCurrency'])

  return Object.entries(filters).filter(([key, value]) => {
    if (ignored.has(key)) return false
    if (Array.isArray(value)) return value.length > 0

    return value !== undefined && value !== ''
  }).length
}
