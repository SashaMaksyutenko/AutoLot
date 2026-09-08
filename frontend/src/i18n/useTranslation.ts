import { useCallback, useSyncExternalStore } from 'react'
import { getLanguage, setLanguage, subscribeToLanguage, type Language } from './languageStore'
import { translate, type MessageValues } from './translate'
import { type MessageKey } from './messages'

interface Translation {
  /** Переклад за ключем: t('nav.buy'), t('auth.checkMailText', { email }). */
  t: (key: MessageKey, values?: MessageValues) => string
  /** Мова, якою зараз говорить інтерфейс. */
  language: Language
  setLanguage: (value: Language) => void
}

/**
 * useSyncExternalStore — вбудований гачок React для стану, що живе ЗА межами
 * React. Йому дають дві функції: як підписатися на зміни й як прочитати
 * поточне значення. React сам перемалює компонент, коли сховище повідомить
 * про зміну.
 *
 * Через це не потрібен ані провайдер, ані контекст: скільки б компонентів
 * не питали мову, вони читають одне спільне сховище.
 */
export function useTranslation(): Translation {
  const language = useSyncExternalStore(subscribeToLanguage, getLanguage)

  /*
    useCallback запам'ятовує функцію між перемальовуваннями й створює НОВУ
    лише тоді, коли змінюється щось із другого аргумента — тут це мова.

    Це не оптимізація, а правильність. React порівнює залежності гачків за
    тотожністю функції. Якби t завжди був тим самим об'єктом, то
    useMemo(() => …, [t]) у майбутньому компоненті НЕ перерахувався б після
    зміни мови — і, скажімо, список варіантів у випадному меню лишився б
    старою мовою.

    Мову передаємо в translate явно, а не лишаємо їй питати сховище: тоді
    компонент напевно намалюється тією мовою, з якою React його викликав.
  */
  const t = useCallback(
    (key: MessageKey, values?: MessageValues) => translate(key, values, language),
    [language],
  )

  return { t, language, setLanguage }
}
