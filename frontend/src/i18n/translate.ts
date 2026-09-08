import { getLanguage, localeOf, type Language } from './languageStore'
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

/**
 * Ключі, у яких є форми множини. Кожен має всі чотири — це перевіряє тип
 * нижче, тож додати «.one» і забути «.many» не вийде.
 */
export type PluralKey =
  | 'catalog.found'
  | 'saved.hint'
  | 'feature.selected'
  | 'listing.views'
  | 'price.basis'
  | 'auction.bids'

/** Усі чотири форми кожного такого ключа мають бути у словнику. */
type PluralForms = `${PluralKey}.${'one' | 'few' | 'many' | 'other'}`

// Рядок нижче нічого не робить під час роботи програми — він існує лише
// заради перевірки типів: якщо якоїсь форми у словнику немає, тут буде
// помилка збірки. «never» ніколи не присвоїться, тому промах видно одразу.
type _EveryFormExists = PluralForms extends MessageKey ? true : never
const _formsChecked: _EveryFormExists = true
void _formsChecked

/**
 * Множина. «1 оголошення, 2 оголошення, 5 оголошень» — в українській три
 * форми, в англійській дві, і правила в них різні.
 *
 * Своїх правил не пишемо: Intl.PluralRules уже вбудований у браузер і знає
 * їх для всіх мов. Він каже, ЯКА форма потрібна для цього числа
 * ('one' | 'few' | 'many' | 'other'), а ми беремо зі словника рядок із
 * такою кінцівкою. Англійська просто ніколи не питає 'few' і 'many'.
 */
export function translatePlural(
  key: PluralKey,
  count: number,
  values?: MessageValues,
  language: Language = getLanguage(),
): string {
  const form = new Intl.PluralRules(localeOf(language)).select(count)
  const candidate = `${key}.${form}`

  // Intl знає ще форми «zero» і «two» — вони є в інших мовах, не в наших.
  // Якщо колись з’явиться мова з ними, впаде не сторінка, а лише точність
  // відмінка: візьмемо загальну форму.
  const resolved = (candidate in dictionaries[language] ? candidate : `${key}.other`) as MessageKey

  return translate(resolved, { count, ...values }, language)
}
