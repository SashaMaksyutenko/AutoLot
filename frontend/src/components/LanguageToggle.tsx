import { useTranslation } from '../i18n/useTranslation'

/**
 * Перемикач мови. Мов рівно дві, тож окремий список зайвий: кнопка показує
 * код тієї мови, НА ЯКУ перемкне — так само, як перемикач теми показує
 * піктограму теми, що буде після натискання.
 *
 * Підпис для незрячих двомовний («Switch to English — перейти на
 * англійську»): людина, яка не читає поточної мови інтерфейсу, має зрозуміти
 * цю кнопку — інакше вона не знайде виходу з мови, якої не знає.
 */
export function LanguageToggle() {
  const { t, language, setLanguage } = useTranslation()

  return (
    <button
      type="button"
      onClick={() => setLanguage(language === 'uk' ? 'en' : 'uk')}
      title={t('language.switch')}
      aria-label={t('language.switch')}
      className="font-mono grid h-[30px] shrink-0 place-items-center rounded-control border border-line bg-surface-2 px-2 text-[11px] font-semibold tracking-wide text-ink-2 hover:text-ink"
    >
      {t('language.other')}
    </button>
  )
}
