import { useQuery } from '@tanstack/react-query'
import { fetchPriceInsight } from '../../api/analytics'
import { formatPrice, plural } from '../../format'

/**
 * Ціна цього авто на тлі ринку.
 *
 * Головне тут — не приховати, наскільки тонка вибірка. «На 12% дешевше за
 * ринок» звучить вагомо, і якщо за цим стоять три оголошення, людина має
 * бачити ці три поруч із цифрою. Тому кількість пишемо завжди й тим самим
 * розміром, що й решту.
 *
 * Коли порівнювати нема з чим, блок не показується взагалі: вигадане
 * порівняння гірше за його відсутність.
 */
export function PriceInsight({ listingId }: { listingId: number }) {
  const insight = useQuery({
    queryKey: ['price-insight', listingId],
    queryFn: ({ signal }) => fetchPriceInsight(listingId, signal),
  })

  if (insight.isPending || insight.isError || !insight.data) {
    return null
  }

  const { market, percentFromMedian } = insight.data
  const cheaper = percentFromMedian < 0
  const size = Math.abs(percentFromMedian)

  return (
    <div className="grid gap-1 rounded-control bg-surface-2 px-3 py-2.5">
      <div className="flex flex-wrap items-baseline gap-x-1.5 text-[13px]">
        {/*
          Відхилення до 5% — це не «дешевше», а звичайний розкид цін.
          Називати його вигодою означало б підказувати неправду.
        */}
        {size < 5 ? (
          <span>Ціна на рівні ринку</span>
        ) : (
          <>
            <strong className={cheaper ? 'text-accent' : 'text-ink'}>
              на {size}% {cheaper ? 'дешевше' : 'дорожче'}
            </strong>
            <span className="text-ink-2">за типову ціну</span>
          </>
        )}
      </div>

      <div className="text-[12.5px] text-ink-2">
        Типова: <span className="font-mono tabular-nums">{formatPrice(market.median, 'Uah')}</span>
      </div>

      {/*
        Рядок сам себе пояснює: скільки авто, яких саме і за який рік.
        Без цього «порахували за 9» стояло поруч зі списком із десяти
        знахідок у каталозі, і різницю доводилося відновлювати самому —
        десяте авто просто іншого року, а роки не змішуються.
      */}
      <div className="text-[12px] text-ink-3">
        За {market.count}{' '}
        {plural(market.count, 'оголошенням', 'оголошеннями', 'оголошеннями')}{' '}
        {market.makeName} {market.modelName}
        {market.year ? ` ${market.year} року` : ' за всі роки'}
        {market.count < 10 ? ' — вибірка мала' : ''}
      </div>
    </div>
  )
}
