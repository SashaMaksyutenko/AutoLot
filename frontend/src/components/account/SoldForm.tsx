import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { fetchBuyerCandidates, markSold } from '../../api/myListings'
import { ApiError } from '../../api/client'
import { formatDateTime } from '../../format'

/**
 * «Кому продали?» — крок між натисканням «Продано» і зміною статусу.
 *
 * Покупця вибирають зі списку тих, хто писав про це авто, а не вводять
 * ім'ям: вручну люди помиляються в іменах, а тезок на майданчику вистачає.
 * «Продав поза AutoLot» — рівноправна відповідь, і саме тому вона тут окремою
 * кнопкою, а не прихована: змушувати вказувати покупця означало б отримати
 * вигаданих.
 */
export function SoldForm({
  listingId,
  onDone,
  onCancel,
}: {
  listingId: number
  onDone: () => void
  onCancel: () => void
}) {
  const [buyerId, setBuyerId] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)

  const candidates = useQuery({
    queryKey: ['buyer-candidates', listingId],
    queryFn: ({ signal }) => fetchBuyerCandidates(listingId, signal),
  })

  const confirm = useMutation({
    mutationFn: (chosen: number | null) => markSold(listingId, chosen),
    onSuccess: onDone,
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : 'Не вдалося зберегти.'),
  })

  const people = candidates.data ?? []
  const winner = people.find((person) => person.isAuctionWinner)

  return (
    <div className="grid gap-2 border-t border-line pt-3">
      <span className="eyebrow">Кому продали?</span>

      {candidates.isPending && <p className="text-[13px] text-ink-2">Завантажуємо…</p>}

      {/* Аукціонний лот: покупця визначили торги, вибирати нема з чого. */}
      {winner ? (
        <p className="text-[13px]">
          Переможець торгів — <strong>{winner.displayName}</strong>.
        </p>
      ) : (
        !candidates.isPending &&
        (people.length === 0 ? (
          <p className="text-[13px] text-ink-2">
            Про це авто вам ніхто не писав, тож покупця зі списку не обрати.
          </p>
        ) : (
          <div className="grid gap-1">
            {people.map((person) => (
              <label
                key={person.id}
                className="flex cursor-pointer items-center gap-2 rounded-control px-2 py-1.5 hover:bg-surface-2"
              >
                <input
                  type="radio"
                  name={`buyer-${listingId}`}
                  checked={buyerId === person.id}
                  onChange={() => setBuyerId(person.id)}
                />
                <span className="text-[13.5px] font-semibold">{person.displayName}</span>
                <span className="text-[12px] text-ink-3">
                  писав {formatDateTime(person.lastMessageAt)}
                </span>
              </label>
            ))}
          </div>
        ))
      )}

      {error && <p className="text-[12px] text-danger">{error}</p>}

      <div className="flex flex-wrap gap-2">
        {winner ? (
          <button
            type="button"
            onClick={() => confirm.mutate(winner.id)}
            disabled={confirm.isPending}
            className="btn btn-primary"
          >
            {confirm.isPending ? 'Зберігаємо…' : 'Продано переможцю'}
          </button>
        ) : (
          <button
            type="button"
            onClick={() => confirm.mutate(buyerId)}
            disabled={confirm.isPending || buyerId === null}
            className="btn btn-primary"
          >
            {confirm.isPending ? 'Зберігаємо…' : 'Продано цій людині'}
          </button>
        )}

        {!winner && (
          <button
            type="button"
            onClick={() => confirm.mutate(null)}
            disabled={confirm.isPending}
            className="btn"
          >
            Продав поза AutoLot
          </button>
        )}

        <button type="button" onClick={onCancel} className="btn">
          Скасувати
        </button>
      </div>
    </div>
  )
}
