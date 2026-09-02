import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { fetchDealReviews, leaveReview, type ReviewRecord } from '../../api/reviews'
import { ApiError } from '../../api/client'
import { formatDateTime } from '../../format'
import { Stars } from './Stars'

/**
 * Відгуки під проданим лотом.
 *
 * Блок з'являється лише тоді, коли є про що говорити: угода відбулася і
 * покупця записано. Під активним оголошенням його немає взагалі — оцінювати
 * ще нічого.
 */
export function DealReviews({ listingId }: { listingId: number }) {
  const queryClient = useQueryClient()

  const state = useQuery({
    queryKey: ['deal-reviews', listingId],
    queryFn: ({ signal }) => fetchDealReviews(listingId, signal),
  })

  if (state.isPending || state.isError || !state.data) {
    return null
  }

  const { canReview, reviews, mineId } = state.data

  // Ні написаного, ні права написати — блок нікому нічого не скаже.
  if (!canReview && reviews.length === 0) {
    return null
  }

  return (
    <section className="card grid gap-3 p-4">
      <h2 className="font-display text-[17px] font-semibold">Відгуки про угоду</h2>

      {reviews.map((review) => (
        <ReviewCard
          key={review.id}
          review={review}
          label={
            review.id === mineId
              ? 'Ваш відгук'
              : review.authorIsSeller
                ? 'Продавець'
                : 'Покупець'
          }
        />
      ))}

      {canReview && (
        <ReviewForm
          listingId={listingId}
          onDone={() => {
            void queryClient.invalidateQueries({ queryKey: ['deal-reviews', listingId] })
            void queryClient.invalidateQueries({ queryKey: ['listing', listingId] })
          }}
        />
      )}
    </section>
  )
}

function ReviewCard({ review, label }: { review: ReviewRecord; label: string }) {
  return (
    <article className="grid gap-1 rounded-control bg-surface-2 px-3 py-2.5">
      <div className="flex flex-wrap items-center gap-2">
        <Stars value={review.rating} />
        <span className="text-[13px] font-semibold">{review.authorName}</span>
        <span className="pill">{label}</span>
        <span className="ml-auto text-[11.5px] text-ink-3">
          {formatDateTime(review.createdAt)}
        </span>
      </div>

      {review.text && <p className="text-[13.5px] whitespace-pre-line">{review.text}</p>}
    </article>
  )
}

function ReviewForm({ listingId, onDone }: { listingId: number; onDone: () => void }) {
  const [rating, setRating] = useState(0)
  const [text, setText] = useState('')
  const [error, setError] = useState<string | null>(null)

  const send = useMutation({
    mutationFn: () => leaveReview(listingId, rating, text.trim()),
    onSuccess: onDone,
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : 'Не вдалося зберегти відгук.'),
  })

  return (
    <form
      className="grid gap-2 border-t border-line pt-3"
      onSubmit={(event) => {
        event.preventDefault()
        setError(null)
        send.mutate()
      }}
    >
      <span className="eyebrow">Як пройшла угода?</span>

      <Stars value={rating} onPick={setRating} />

      <textarea
        value={text}
        onChange={(event) => setText(event.target.value)}
        rows={3}
        maxLength={1000}
        placeholder="Кілька слів (не обов'язково)"
        className="control resize-y"
      />

      {/* Попереджаємо ДО натискання: після збереження виправити не вийде. */}
      <p className="text-[11.5px] text-ink-3">
        Відгук публічний, і змінити його потім не можна.
      </p>

      {error && <p className="text-[12px] text-danger">{error}</p>}

      <button
        type="submit"
        disabled={send.isPending || rating === 0}
        className="btn btn-primary justify-self-start"
      >
        {send.isPending ? 'Зберігаємо…' : 'Лишити відгук'}
      </button>
    </form>
  )
}
