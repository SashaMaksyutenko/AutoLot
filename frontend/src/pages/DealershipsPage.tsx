import { useState } from 'react'
import { Link } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { fetchDealerships, type DealershipCard } from '../api/dealership'
import { VerifiedMark } from '../components/catalog/ListingCard'
import { formatCount, plural } from '../format'

/**
 * Каталог автосалонів. Перевірені йдуть першими — саме заради цього бейдж
 * і потрібен; порядок задає бекенд, тут його не переставляємо.
 */
export function DealershipsPage() {
  const [text, setText] = useState('')
  const [verifiedOnly, setVerifiedOnly] = useState(false)

  const dealerships = useQuery({
    queryKey: ['dealerships', text, verifiedOnly],
    queryFn: ({ signal }) => fetchDealerships({ text, verifiedOnly }, signal),
    placeholderData: keepPreviousData,
  })

  return (
    <div className="wrap grid gap-3.5 py-[26px]">
      <div>
        <h1 className="font-display text-[25px] font-bold">Автосалони</h1>
        <p className="text-[13px] text-ink-2">
          {dealerships.isPending
            ? 'Завантажуємо…'
            : `${formatCount(dealerships.data?.length ?? 0)} ${plural(
                dealerships.data?.length ?? 0,
                'салон',
                'салони',
                'салонів',
              )}`}
        </p>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <input
          value={text}
          onChange={(event) => setText(event.target.value)}
          placeholder="Назва салону"
          className="control max-w-[320px]"
        />

        <label className="flex cursor-pointer items-center gap-2 text-[13.5px]">
          <input
            type="checkbox"
            checked={verifiedOnly}
            onChange={(event) => setVerifiedOnly(event.target.checked)}
            className="h-[15px] w-[15px] accent-accent"
          />
          <span>Лише перевірені</span>
        </label>
      </div>

      {dealerships.data?.length === 0 && (
        <p className="card p-10 text-center text-sm text-ink-2">
          Салонів за такими умовами немає.
        </p>
      )}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {dealerships.data?.map((dealership) => (
          <DealerTile key={dealership.id} dealership={dealership} />
        ))}
      </div>
    </div>
  )
}

function DealerTile({ dealership }: { dealership: DealershipCard }) {
  return (
    <Link to={`/dealers/${dealership.slug}`} className="block">
      <article className="card grid gap-2 p-4 transition hover:-translate-y-px hover:border-ink-3">
        <div className="flex items-start gap-3">
          <Logo dealership={dealership} />

          <div className="min-w-0">
            <h2 className="font-display flex items-center gap-1.5 text-[16px] font-semibold">
              {dealership.isVerified && <VerifiedMark />}
              <span className="truncate">{dealership.name}</span>
            </h2>
            <p className="truncate text-[12.5px] text-ink-2">{dealership.cityName}</p>
          </div>
        </div>

        <div className="border-t border-line pt-2 text-[13px] text-ink-2">
          <span className="font-mono font-semibold text-ink tabular-nums">
            {formatCount(dealership.activeListingCount)}
          </span>{' '}
          {plural(dealership.activeListingCount, 'авто', 'авто', 'авто')}
        </div>
      </article>
    </Link>
  )
}

/** Логотип салону або перша літера назви, поки логотипа немає. */
function Logo({ dealership }: { dealership: DealershipCard }) {
  if (dealership.logoPath) {
    return (
      <img
        src={`/media/${dealership.logoPath}`}
        alt=""
        className="h-11 w-11 shrink-0 rounded-control border border-line object-cover"
      />
    )
  }

  return (
    <span className="font-display grid h-11 w-11 shrink-0 place-items-center rounded-control bg-surface-3 text-[18px] font-bold text-ink-2">
      {dealership.name.charAt(0).toUpperCase()}
    </span>
  )
}
