import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { fetchPublicProfile } from '../../api/users'
import { RatingLine } from '../listing/Stars'

/**
 * Власна репутація в кабінеті.
 *
 * Береться з ПУБЛІЧНОГО профілю навмисно — з того самого, що бачать
 * сторонні. Так людина дивиться на себе їхніми очима, а не на окремі
 * «внутрішні» цифри, які могли б із ними розійтися.
 */
export function MyReputation({ userId }: { userId: number }) {
  const profile = useQuery({
    queryKey: ['public-profile', userId],
    queryFn: ({ signal }) => fetchPublicProfile(userId, signal),
  })

  if (profile.isPending || profile.isError || !profile.data) {
    return null
  }

  const { rating } = profile.data

  return (
    <section className="card grid gap-2 p-4">
      <span className="eyebrow">Ваша репутація</span>

      <RatingLine count={rating.count} average={rating.average} size={16} />

      {rating.count === 0 ? (
        <p className="text-[12.5px] text-ink-3">
          Відгуки з'являються після завершених угод — від тих, хто у вас купив
          або вам продав.
        </p>
      ) : (
        <Link to={`/users/${userId}`} className="text-[12.5px] text-accent hover:underline">
          Подивитися, як це бачать інші
        </Link>
      )}
    </section>
  )
}
