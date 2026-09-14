import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { fetchRecentlyViewed } from '../api/history'
import { useAuth } from '../auth/useAuth'
import { ListingCard } from './catalog/ListingCard'
import { useTranslation } from '../i18n/useTranslation'

/** Скільки показувати в підказці збоку. Рівно один ряд на широкому екрані. */
const Preview = 4

/**
 * Історія переглядів — коротка.
 *
 * Стоїть у трьох місцях: під карткою авто, у видачі каталогу й у кабінеті.
 * Скрізь показує один ряд, а заголовок веде на сторінку з усією історією:
 * тут це підказка збоку, і розгортати її на пів екрана нема потреби.
 *
 * Порожню секцію не малюємо взагалі — новачок побачив би рамку з написом
 * «тут нічого немає», і це єдине, що він про неї дізнався б.
 */
export function RecentlyViewed({ excludeId }: { excludeId?: number }) {
  const { t } = useTranslation()
  const auth = useAuth()

  /*
    Просимо на одне більше, ніж покажемо: поточне авто ми з переліку
    приберемо, і без запасу останній ряд виявився б неповним саме там,
    де історія найдовша.
  */
  const take = Preview + 1

  const viewed = useQuery({
    queryKey: ['recently-viewed', take],
    queryFn: ({ signal }) => fetchRecentlyViewed(take, signal),

    // Гостю історія не ведеться, тож і питати нема чого.
    enabled: auth.user !== null,
  })

  const items = (viewed.data ?? [])
    .filter((listing) => listing.id !== excludeId)
    .slice(0, Preview)

  if (items.length === 0) return null

  return (
    <section className="grid gap-3">
      <h2 className="font-display text-[19px] font-bold">
        <Link to="/viewed" className="hover:text-accent hover:underline">
          {t('viewed.title')}
        </Link>
      </h2>

      <div className="grid gap-3.5 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        {items.map((listing) => (
          <ListingCard key={listing.id} listing={listing} />
        ))}
      </div>
    </section>
  )
}
