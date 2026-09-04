import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { fetchFeatures } from '../../api/reference'
import { plural } from '../../format'

/**
 * Вибір опцій комплектації: підігрів сидінь, камера, фаркоп.
 *
 * Довідник із сотнею позначок не можна показати плоским списком — панель
 * фільтрів перетворилася б на кілометр прокрутки. Тому категорії згорнуті,
 * а розгортає їх людина сама, коли шукає щось конкретне.
 *
 * Важливо, як ці опції діють на пошук: авто має мати **всі** обрані одразу,
 * а не будь-яку з них. «З підігрівом І з камерою» — це звуження, і саме
 * такого від такого фільтра чекають.
 */
export function FeaturePicker({
  selected,
  onChange,
}: {
  selected: number[]
  onChange: (featureIds: number[]) => void
}) {
  const [openCategory, setOpenCategory] = useState<string | null>(null)

  const features = useQuery({
    queryKey: ['features'],
    queryFn: ({ signal }) => fetchFeatures(signal),
    staleTime: Infinity,
  })

  const groups = features.data ?? []

  if (groups.length === 0) {
    return null
  }

  function toggle(id: number) {
    onChange(
      selected.includes(id) ? selected.filter((value) => value !== id) : [...selected, id],
    )
  }

  return (
    <div className="grid gap-1">
      {selected.length > 0 && (
        <div className="flex items-baseline justify-between gap-2 pb-1">
          <span className="text-[12px] text-ink-2">
            обрано {selected.length}{' '}
            {plural(selected.length, 'опція', 'опції', 'опцій')}
          </span>
          <button
            type="button"
            onClick={() => onChange([])}
            className="text-[12px] text-accent hover:underline"
          >
            зняти
          </button>
        </div>
      )}

      {groups.map((group) => {
        const open = openCategory === group.category
        const chosenHere = group.features.filter((item) => selected.includes(item.id)).length

        return (
          <div key={group.category}>
            <button
              type="button"
              onClick={() => setOpenCategory(open ? null : group.category)}
              className="flex w-full items-center gap-1.5 py-1 text-left text-[13px] hover:text-accent"
            >
              {/* Стрілка повертається — найзрозуміліший знак «тут щось згорнуте». */}
              <span
                className={`text-ink-3 transition-transform ${open ? 'rotate-90' : ''}`}
                aria-hidden="true"
              >
                ›
              </span>

              <span className="flex-1">{categoryLabels[group.category] ?? group.category}</span>

              {chosenHere > 0 && (
                <span className="font-mono text-[11.5px] text-accent tabular-nums">
                  {chosenHere}
                </span>
              )}
            </button>

            {open && (
              <div className="grid gap-0.5 pb-1 pl-4">
                {group.features.map((item) => (
                  <label
                    key={item.id}
                    className="flex cursor-pointer items-center gap-2 text-[12.5px]"
                  >
                    <input
                      type="checkbox"
                      checked={selected.includes(item.id)}
                      onChange={() => toggle(item.id)}
                    />
                    {item.name}
                  </label>
                ))}
              </div>
            )}
          </div>
        )
      })}
    </div>
  )
}

/**
 * Назви категорій. Сама категорія приходить кодом (`Comfort`), бо це значення
 * переліку в коді, а не рядок довідника — на відміну від назв самих опцій,
 * які сервер уже переклав.
 */
const categoryLabels: Record<string, string> = {
  Interior: 'Салон',
  Comfort: 'Комфорт',
  Safety: 'Безпека',
  Body: 'Кузов',
  Multimedia: 'Мультимедіа',
}
