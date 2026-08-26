import { useEffect, useRef, useState } from 'react'

/**
 * Скільки лишилося до кінця торгів — за годинником СЕРВЕРА, а не пристрою.
 *
 * Навіщо так складно: годинник на комп'ютері чи телефоні легко відстає або
 * поспішає на кілька хвилин, і людина побачила б «лишилося 3 хвилини», коли
 * торги вже скінчилися. Тому джерело істини — сервер (SPEC §5): він разом із
 * кожною відповіддю каже свій час, а ми запам'ятовуємо різницю й малюємо
 * таймер із поправкою.
 *
 * Похибка лишається — поки відповідь летіла мережею, серверний час трохи
 * застарів. Це десяті частки секунди, і для таймера, що цокає раз на секунду,
 * вони не мають значення.
 */
export function useCountdown(endsAt: string, serverTime: string): number {
  /*
    useRef зберігає значення між перемальовуваннями, але, на відміну від
    useState, його зміна НЕ викликає перемальовування. Саме те, що треба:
    поправка — це службова величина, а не те, що видно на екрані.
  */
  const offset = useRef(0)

  const [remaining, setRemaining] = useState(() => untilEnd(endsAt, 0))

  useEffect(() => {
    offset.current = new Date(serverTime).getTime() - Date.now()
    setRemaining(untilEnd(endsAt, offset.current))
  }, [serverTime, endsAt])

  useEffect(() => {
    const timer = setInterval(() => {
      setRemaining(untilEnd(endsAt, offset.current))
    }, 1000)

    // Прибираємо таймер, коли компонент зникає з екрана. Без цього він
    // продовжив би цокати й смикати вже неіснуючий стан.
    return () => clearInterval(timer)
  }, [endsAt])

  return remaining
}

function untilEnd(endsAt: string, offset: number): number {
  return Math.max(0, new Date(endsAt).getTime() - (Date.now() + offset))
}

/**
 * «02:14:33», а за менш ніж добу до кінця — без днів. Секунди показуємо
 * завжди: під кінець торгів саме вони й важливі.
 */
export function formatRemaining(milliseconds: number): string {
  if (milliseconds <= 0) return 'торги завершено'

  const total = Math.floor(milliseconds / 1000)
  const days = Math.floor(total / 86_400)
  const hours = Math.floor((total % 86_400) / 3_600)
  const minutes = Math.floor((total % 3_600) / 60)
  const seconds = total % 60

  const pad = (value: number) => String(value).padStart(2, '0')

  return days > 0
    ? `${days} дн ${pad(hours)}:${pad(minutes)}:${pad(seconds)}`
    : `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`
}
