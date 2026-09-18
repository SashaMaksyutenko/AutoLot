import { useQuery } from '@tanstack/react-query'
import { fetchPriceHistory, type PriceHistoryPoint } from '../../api/priceHistory'
import { formatDate, formatPrice } from '../../format'
import { useTranslation } from '../../i18n/useTranslation'

/** Розміри полотна графіка в його власних одиницях — не пікселях. */
const Canvas = { width: 240, height: 48, padding: 4 }

/**
 * Як мінялася ціна цього авто.
 *
 * Найкорисніше про ціну на класифайді — не саме число, а те, як воно
 * поводилося. Авто, яке місяць стоїть без руху, і авто, яке за той самий
 * місяць подешевшало тричі, — дві дуже різні розмови з продавцем.
 *
 * Поки ціна не мінялася, блок не показуємо взагалі: графік з однієї точки —
 * це порожня рамка з написом «змін не було», і єдине, що людина з неї
 * дізнається, — що ми вміємо малювати рамки.
 */
export function PriceHistory({ listingId }: { listingId: number }) {
  const { t } = useTranslation()

  const history = useQuery({
    queryKey: ['price-history', listingId],
    queryFn: ({ signal }) => fetchPriceHistory(listingId, signal),
  })

  const points = history.data ?? []

  if (points.length < 2) {
    return null
  }

  const first = points[0]
  const last = points[points.length - 1]

  // Порівнюємо в гривні: валюту оголошення могли змінити дорогою.
  const cheaper = last.priceUah < first.priceUah
  const difference = Math.abs(last.priceUah - first.priceUah)
  const percent = Math.round((difference / first.priceUah) * 100)

  return (
    <details className="rounded-control bg-surface-2 px-3 py-2.5">
      {/*
        details/summary — вбудований у браузер розкривний блок. Без жодного
        JavaScript: клік по summary показує чи ховає решту. Для другорядного
        вмісту це рівно те, що треба.
      */}
      <summary className="flex cursor-pointer flex-wrap items-baseline gap-x-1.5 text-[13px]">
        <strong className={cheaper ? 'text-accent' : 'text-ink'}>
          {cheaper
            ? t('priceHistory.fell', { percent })
            : t('priceHistory.rose', { percent })}
        </strong>
        <span className="text-ink-2">
          {t('priceHistory.since', { date: formatDate(first.changedAt) })}
        </span>
      </summary>

      <div className="mt-2.5 grid gap-2">
        <Sparkline points={points} cheaper={cheaper} />

        <ol className="grid gap-0.5 font-mono text-[12.5px] tabular-nums">
          {/*
            Найсвіжіше зверху: людина читає згори вниз, і перше, що їй
            цікаво, — нинішня ціна, а не та, з якої все починалося.
          */}
          {[...points].reverse().map((point) => (
            <li key={point.changedAt} className="flex justify-between gap-3">
              <span className="text-ink-2">{formatDate(point.changedAt)}</span>
              <span>{formatPrice(point.price, point.currency)}</span>
            </li>
          ))}
        </ol>
      </div>
    </details>
  )
}

/**
 * Лінія ціни. Малюємо самі, без бібліотеки графіків: точок одиниці, осей і
 * підказок тут не треба, а будь-яка бібліотека важила б більше за всю решту
 * сторінки.
 *
 * SVG зручний тим, що координати в ньому власні (viewBox), а не піксельні:
 * ми рахуємо в зрозумілих числах, а браузер сам розтягує малюнок під ширину,
 * яку йому дали.
 */
function Sparkline({ points, cheaper }: { points: PriceHistoryPoint[]; cheaper: boolean }) {
  const values = points.map((point) => point.priceUah)
  const lowest = Math.min(...values)
  const highest = Math.max(...values)

  // Якщо всі значення однакові, різниця нульова — ділити на неї не можна.
  const spread = highest - lowest || 1

  const usable = {
    width: Canvas.width - Canvas.padding * 2,
    height: Canvas.height - Canvas.padding * 2,
  }

  const path = values
    .map((value, index) => {
      const x = Canvas.padding + (index / (values.length - 1)) * usable.width

      // Вісь Y у SVG росте ВНИЗ, тому найбільше значення має дати найменший y.
      const y = Canvas.padding + (1 - (value - lowest) / spread) * usable.height

      return `${index === 0 ? 'M' : 'L'} ${x.toFixed(1)} ${y.toFixed(1)}`
    })
    .join(' ')

  return (
    <svg
      viewBox={`0 0 ${Canvas.width} ${Canvas.height}`}
      className="h-12 w-full"
      preserveAspectRatio="none"

      // Малюнок нічого не додає до тексту поруч — для читача екрана він шум.
      aria-hidden="true"
    >
      <path
        d={path}
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        className={cheaper ? 'text-accent' : 'text-ink-3'}

        // vectorEffect не дає лінії потовщати, коли SVG розтягують по ширині.
        vectorEffect="non-scaling-stroke"
      />
    </svg>
  )
}
