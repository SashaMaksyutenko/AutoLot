import { Link, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { fetchPublicProfile } from '../api/users'
import { fetchReviewsAbout, type ReviewRecord } from '../api/reviews'
import { VerifiedMark } from '../components/catalog/ListingCard'
import { RatingLine, Stars } from '../components/listing/Stars'
import { formatDateTime, formatMonthYear } from '../format'
import { useTranslation } from '../i18n/useTranslation'

/**
 * Публічний профіль продавця.
 *
 * З'явився не заради повноти: без нього рейтинг було видно лише випадково —
 * якщо відкрити котресь із оголошень людини. Репутація, яку не можна
 * подивитися навмисно, репутацією не працює.
 *
 * Пошти й телефону тут немає. Не тому, що сторінка їх ховає, а тому, що
 * сервер їх не віддає: зв'язатися з продавцем можна лише через оголошення.
 */
export function UserProfilePage() {
  const { t, tPlural } = useTranslation()

  const { id } = useParams()
  const userId = Number(id)

  const profile = useQuery({
    queryKey: ['public-profile', userId],
    queryFn: ({ signal }) => fetchPublicProfile(userId, signal),
    enabled: Number.isFinite(userId),
  })

  const reviews = useQuery({
    queryKey: ['reviews-about', userId],
    queryFn: ({ signal }) => fetchReviewsAbout(userId, signal),
    enabled: Number.isFinite(userId),
  })

  if (profile.isPending) {
    return <Notice>{t('profile.loading')}</Notice>
  }

  if (profile.isError || !profile.data) {
    return <Notice>{t('profile.notFound')}</Notice>
  }

  const person = profile.data
  const written = reviews.data ?? []

  return (
    <div className="wrap grid items-start gap-[22px] py-[26px] lg:grid-cols-[minmax(0,1fr)_320px]">
      <main className="grid gap-4">
        <section className="card grid gap-2 p-4">
          <h1 className="font-display text-[25px] font-bold">{person.displayName}</h1>

          <div className="flex flex-wrap items-center gap-2 text-[13px] text-ink-2">
            <span className="pill">
              {person.accountType === 'Dealer' ? t('profile.dealer') : t('profile.private')}
            </span>

            {person.dealer && (
              <Link
                to={`/dealers/${person.dealer.slug}`}
                className="flex items-center gap-1 hover:text-accent"
              >
                {person.dealer.isVerified && <VerifiedMark size={13} />}
                {person.dealer.name}
              </Link>
            )}

            {person.cityName && <span>{person.cityName}</span>}
          </div>

          <RatingLine count={person.rating.count} average={person.rating.average} size={16} />

          <p className="text-[12.5px] text-ink-3">
            {t('profile.joined', { date: formatMonthYear(person.joinedAt) })}
            {tPlural('profile.active', person.activeListingCount)}
          </p>
        </section>

        <section className="grid gap-3">
          <h2 className="font-display text-[19px] font-bold">
            {written.length > 0
              ? t('profile.reviewsCount', { count: written.length })
              : t('profile.reviews')}
          </h2>

          {reviews.isPending && (
            <p className="card p-6 text-sm text-ink-2">{t('profile.loading')}</p>
          )}

          {!reviews.isPending && written.length === 0 && (
            <p className="card p-8 text-center text-sm text-ink-2">
              {t('profile.noReviews')}
            </p>
          )}

          {written.map((review) => (
            <ReviewCard key={review.id} review={review} />
          ))}
        </section>
      </main>

      <aside className="card grid gap-2 p-4 text-[13px] text-ink-2 lg:sticky lg:top-[74px]">
        <span className="eyebrow">{t('profile.howToRead')}</span>
        <p>
          {t('profile.howToRead1')}
        </p>
        <p>
          {t('profile.howToRead2')}
        </p>
      </aside>
    </div>
  )
}

function ReviewCard({ review }: { review: ReviewRecord }) {
  const { t } = useTranslation()

  return (
    <article className="card grid gap-1.5 p-3">
      <div className="flex flex-wrap items-center gap-2">
        <Stars value={review.rating} />
        <span className="text-[13.5px] font-semibold">{review.authorName}</span>
        <span className="pill">
        {review.authorIsSeller ? t('profile.fromSeller') : t('profile.fromBuyer')}
      </span>
        <span className="ml-auto text-[11.5px] text-ink-3">
          {formatDateTime(review.createdAt)}
        </span>
      </div>

      {review.text && <p className="text-[13.5px] whitespace-pre-line">{review.text}</p>}

      {/* Посилання на сам лот: без нього відгук — слова без приводу. */}
      <Link
        to={`/listing/${review.listingId}`}
        className="text-[12px] text-ink-3 hover:text-accent"
      >
        {review.listingTitle}
      </Link>
    </article>
  )
}

function Notice({ children }: { children: React.ReactNode }) {
  return (
    <div className="wrap py-16">
      <p className="card mx-auto max-w-[460px] p-10 text-center text-sm text-ink-2">{children}</p>
    </div>
  )
}
