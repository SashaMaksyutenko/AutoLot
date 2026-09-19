import { useQuery } from '@tanstack/react-query'
import { fetchSimilar } from '../../api/similar'
import { useTranslation } from '../../i18n/useTranslation'
import { ListingCard } from '../catalog/ListingCard'

/** Один ряд на широкому екрані — як і в історії переглядів поруч. */
const Count = 4

/**
 * Схожі авто під карткою.
 *
 * Без цього блоку сторінка авто — глухий кут: людина або купує, або
 * повертається до каталогу й шукає наново. Особливо це болить на проданому
 * авто: той, хто його відкрив, саме й шукає, що купити замість нього.
 *
 * Порожнього блоку не малюємо: рамка з написом «схожих немає» нічого не дає
 * тому, хто її бачить.
 */
export function SimilarListings({ listingId }: { listingId: number }) {
  const { t } = useTranslation()

  const similar = useQuery({
    queryKey: ['similar', listingId],
    queryFn: ({ signal }) => fetchSimilar(listingId, Count, signal),
  })

  const items = similar.data ?? []

  if (items.length === 0) return null

  return (
    <section className="grid gap-3">
      <h2 className="font-display text-[19px] font-bold">{t('similar.title')}</h2>

      <div className="grid gap-3.5 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        {items.map((listing) => (
          <ListingCard key={listing.id} listing={listing} />
        ))}
      </div>
    </section>
  )
}
