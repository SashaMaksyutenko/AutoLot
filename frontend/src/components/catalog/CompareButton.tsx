import { useState } from 'react'
import { compareLimit } from '../../compare/compareStore'
import { useCompare } from '../../compare/useCompare'

/**
 * «Порівняти» на картці авто.
 *
 * Лежить усередині посилання на оголошення, тож натискання доводиться
 * зупиняти вручну: інакше клік і відклав би авто, і одразу відкрив би його
 * сторінку, з якої людина вже не бачить, що саме сталося.
 */
export function CompareButton({ listingId }: { listingId: number }) {
  const compare = useCompare()
  const [full, setFull] = useState(false)

  const chosen = compare.isCompared(listingId)

  return (
    <button
      type="button"
      onClick={(event) => {
        // Картка — це посилання. Без цих двох рядків браузер перейшов би
        // на сторінку авто одразу після натискання.
        event.preventDefault()
        event.stopPropagation()

        setFull(!compare.toggle(listingId))
      }}
      title={
        full
          ? `Більше ${compareLimit} авто порівнювати нема сенсу — не влазить на екран`
          : chosen
            ? 'Прибрати з порівняння'
            : 'Додати до порівняння'
      }
      aria-pressed={chosen}
      className={`grid h-[30px] w-[30px] place-items-center rounded-control border backdrop-blur-sm ${
        chosen
          ? 'border-accent bg-accent-soft text-accent'
          : full
            ? 'border-danger text-danger'
            : 'border-line bg-surface/80 text-ink-2 hover:text-ink'
      }`}
    >
      <ScalesIcon />
    </button>
  )
}

/**
 * Терези — усталений знак порівняння. Розміткою, а не картинкою: так значок
 * успадковує колір тексту й міняється разом зі станом кнопки.
 */
function ScalesIcon() {
  return (
    <svg
      viewBox="0 0 16 16"
      width="14"
      height="14"
      aria-hidden="true"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M8 2.5v11M4 4.2h8M2.2 9.2h3.6L4 5.4 2.2 9.2ZM10.2 9.2h3.6L12 5.4l-1.8 3.8Z" />
      <path d="M5.5 13.5h5" />
    </svg>
  )
}
