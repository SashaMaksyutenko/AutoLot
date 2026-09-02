import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { fetchDealReviews } from '../../api/reviews'

/**
 * «Оцініть угоду» — нагадування там, де людина бачить свій проданий чи
 * куплений лот.
 *
 * Без цього рядка відгуків просто не буде. Щоб лишити відгук, треба було
 * самому згадати про давню угоду й відкрити сторінку авто — так не робить
 * ніхто. Механізм відгуків без нагадування збирає порожнечу.
 *
 * Запит іде на кожен проданий рядок окремо, і це свідомо: продані лоти в
 * однієї людини рахуються одиницями, а спільний ендпоінт «де я можу лишити
 * відгук» довелося б тримати синхронним із правилами доступу в двох місцях.
 */
export function ReviewPrompt({ listingId }: { listingId: number }) {
  const state = useQuery({
    queryKey: ['deal-reviews', listingId],
    queryFn: ({ signal }) => fetchDealReviews(listingId, signal),
  })

  if (state.isPending || state.isError || !state.data) {
    return null
  }

  const { canReview, mineId } = state.data

  if (canReview) {
    return (
      <Link
        to={`/listing/${listingId}`}
        className="rounded-control bg-accent-soft px-2.5 py-2 text-[12.5px] font-semibold text-accent hover:underline"
      >
        Оцініть угоду — відгук лишається лише раз
      </Link>
    )
  }

  // Уже написаний відгук показуємо теж: інакше рядок просто зникав би, і
  // людина не знала б, чи вона вже писала.
  if (mineId !== null) {
    return (
      <span className="px-1 text-[12px] text-ink-3">Ви вже оцінили цю угоду</span>
    )
  }

  return null
}
