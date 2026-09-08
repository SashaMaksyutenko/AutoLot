import { useQuery } from '@tanstack/react-query'
import { fetchPriceInsight } from '../../api/analytics'
import { formatPrice } from '../../format'
import { useTranslation } from '../../i18n/useTranslation'

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
  const { t, tPlural } = useTranslation()

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
          <span>{t('price.atMarket')}</span>
        ) : (
          <>
            <strong className={cheaper ? 'text-accent' : 'text-ink'}>
              {cheaper ? t('price.cheaperBy', { size }) : t('price.dearerBy', { size })}
            </strong>
            <span className="text-ink-2">{t('price.versusTypical')}</span>
          </>
        )}
      </div>

      <div className="text-[12.5px] text-ink-2">
        {t('price.typical')}{' '}
        <span className="font-mono tabular-nums">{formatPrice(market.median, 'Uah')}</span>
      </div>

      {/*
        Рядок сам себе пояснює: скільки авто, яких саме і за який рік.
        Без цього «порахували за 9» стояло поруч зі списком із десяти
        знахідок у каталозі, і різницю доводилося відновлювати самому —
        десяте авто просто іншого року, а роки не змішуються.
      */}
      <div className="text-[12px] text-ink-3">
        {tPlural('price.basis', market.count, {
          model: `${market.makeName} ${market.modelName}`,
          period: market.year
            ? t('price.periodYear', { year: market.year })
            : t('price.periodAllYears'),
          note: market.count < 10 ? t('price.smallSample') : '',
        })}
      </div>
    </div>
  )
}
