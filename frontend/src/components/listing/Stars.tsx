/**
 * Зірки рейтингу — і для показу, і для вибору оцінки.
 *
 * Один компонент на два випадки навмисно: намальовані зірки мають виглядати
 * однаково там, де їх читають, і там, де ставлять. Різниця лише в тому, чи
 * передали `onPick`.
 *
 * Дробову оцінку не малюємо половинками — округлюємо до найближчої зірки, а
 * точне число показуємо поруч цифрою. Половина зірки нічого не додає до
 * розуміння, зате помітно ускладнює розмітку.
 */
export function Stars({
  value,
  size = 15,
  onPick,
}: {
  value: number
  size?: number
  onPick?: (rating: number) => void
}) {
  const filled = Math.round(value)

  return (
    <span className="inline-flex items-center gap-0.5">
      {[1, 2, 3, 4, 5].map((position) =>
        onPick ? (
          <button
            key={position}
            type="button"
            onClick={() => onPick(position)}
            aria-label={`Оцінка ${position} з 5`}
            className="leading-none"
          >
            <Star on={position <= filled} size={size + 5} />
          </button>
        ) : (
          <Star key={position} on={position <= filled} size={size} />
        ),
      )}
    </span>
  )
}

/**
 * Одна зірка. Заповнена — суцільна, порожня — лише контур: так вони
 * розрізняються навіть на чорно-білому екрані й для тих, хто не бачить
 * кольору.
 */
function Star({ on, size }: { on: boolean; size: number }) {
  return (
    <svg
      viewBox="0 0 20 20"
      width={size}
      height={size}
      aria-hidden="true"
      className={on ? 'text-signal' : 'text-ink-3'}
      fill={on ? 'currentColor' : 'none'}
      stroke="currentColor"
      strokeWidth="1.4"
      strokeLinejoin="round"
    >
      <path d="M10 2.5l2.35 4.76 5.25.76-3.8 3.7.9 5.23L10 14.48l-4.7 2.47.9-5.23-3.8-3.7 5.25-.76z" />
    </svg>
  )
}

/** Рейтинг рядком: зірки, число й кількість відгуків. */
export function RatingLine({
  count,
  average,
  size = 14,
}: {
  count: number
  average: number
  size?: number
}) {
  if (count === 0) {
    // «0,0» читалося б як погана оцінка, хоча оцінок просто немає.
    return <span className="text-[12.5px] text-ink-3">Ще без відгуків</span>
  }

  return (
    <span className="inline-flex items-center gap-1.5">
      <Stars value={average} size={size} />
      <span className="font-mono text-[12.5px] tabular-nums">
        {average.toFixed(1)}
      </span>
      <span className="text-[12px] text-ink-3">({count})</span>
    </span>
  )
}
