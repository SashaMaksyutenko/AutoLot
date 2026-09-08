import { getLanguage, type Language } from './languageStore'
import { dictionaries, type MessageKey } from './messages'

/** Значення для підстановок на кшталт «{email}» усередині рядка. */
export type MessageValues = Record<string, string | number>

/**
 * Бере рядок зі словника й підставляє значення.
 *
 * Функція навмисно звичайна, а не гачок: її кличе й код, який до React не
 * має стосунку — обгортка над fetch, наприклад. Гачок там викликати
 * заборонено, а текст помилки людині показати треба.
 *
 * Мову можна передати третім аргументом. Без нього береться поточна — так
 * зручно поза React; а компонент передає ту мову, з якою він саме
 * малюється, і тоді на екрані напевно не змішаються дві.
 */
export function translate(
  key: MessageKey,
  values?: MessageValues,
  language: Language = getLanguage(),
): string {
  const template = dictionaries[language][key]

  if (!values) return template

  /*
    replace із регулярним виразом: /\{(\w+)\}/g знаходить усі «{слово}»
    у рядку (g — усі, а не лише перше), а другий аргумент-функція отримує
    саме слово й повертає те, на що його замінити. Невідоме ім'я лишаємо
    як є: краще побачити на екрані «{email}» і зрозуміти, де помилка,
    ніж мовчазну порожнечу.
  */
  return template.replace(/\{(\w+)\}/g, (whole, name: string) =>
    name in values ? String(values[name]) : whole,
  )
}
