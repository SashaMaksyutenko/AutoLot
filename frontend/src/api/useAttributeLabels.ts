import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { fetchCarAttributes, type CarAttributes } from './reference'

type AttributeKind = keyof CarAttributes

/**
 * Перетворює значення переліку на назву мовою користувача.
 *
 * Оголошення приходить із сирим значенням — `fuelType: "Petrol"`, — бо саме
 * його доведеться надіслати назад у фільтрі. Назву для показу віддає
 * довідник `/api/cars/attributes`, уже перекладену сервером. Тут ці дві
 * половини зводяться докупи.
 *
 * Довідник кешується назавжди й тягнеться один раз на весь застосунок:
 * запит той самий, що й у панелі фільтрів, тож повторного звернення до
 * мережі не буде.
 */
export function useAttributeLabels() {
  const { data } = useQuery({
    queryKey: ['car-attributes'],
    queryFn: ({ signal }) => fetchCarAttributes(signal),
    staleTime: Infinity,
  })

  return useMemo(() => {
    const labels = new Map<string, string>()

    if (data) {
      for (const [kind, items] of Object.entries(data)) {
        for (const item of items) {
          labels.set(`${kind}:${item.value}`, item.name)
        }
      }
    }

    // Поки довідник вантажиться, показуємо сире значення — краще за порожнє
    // місце, і рядок не стрибає, коли назва приїде.
    return (kind: AttributeKind, value: string) => labels.get(`${kind}:${value}`) ?? value
  }, [data])
}
