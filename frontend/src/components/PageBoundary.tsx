import { Component, type ErrorInfo, type ReactNode } from 'react'
import { useTranslation } from '../i18n/useTranslation'

/**
 * Ловить помилку сторінки й показує замість білого екрана щось осмислене.
 *
 * Навіщо саме тепер. Відколи сторінки вантажаться окремими файлами, з'явився
 * новий спосіб зламатися: вкладку відкрили вчора, відтоді сайт оновили, і
 * файл зі старим іменем на сервері вже не лежить. Перехід на іншу сторінку
 * тоді просто нічого не намалює — без запобіжника людина побачить порожнечу
 * й не здогадається, що допомогло б звичайне оновлення.
 *
 * Чому клас, а не функція. Перехоплювати помилки вміє лише компонент-клас:
 * React викликає в нього два особливих методи, а серед гачків рівноцінного
 * немає. Це єдиний клас у проєкті — і він тут не з примхи, а з потреби.
 */
export class PageBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false }

  /*
    React кличе це, коли нижче щось впало, і бере повернений об'єкт як новий
    стан. «static» означає, що метод належить самому класу, а не окремому
    компоненту: на цю мить робочого примірника ще може не бути.
  */
  static getDerivedStateFromError() {
    return { failed: true }
  }

  /*
    А сюди React передає подробиці — вже після того, як намалював запасний
    вигляд. Місце для запису в журнал; у консоль пишемо саму помилку, і нічого
    зі своїх даних до неї не додаємо.
  */
  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Сторінку не вдалося показати:', error, info.componentStack)
  }

  render() {
    return this.state.failed ? <LoadFailed /> : this.props.children
  }
}

/**
 * Сам напис винесено в окрему функцію, бо переклад дає гачок, а гачки в
 * класах не працюють. Клас вирішує, КОЛИ показати; функція — ЩО показати.
 */
function LoadFailed() {
  const { t } = useTranslation()

  return (
    <div className="wrap grid justify-items-center gap-3 py-16 text-center">
      <p className="text-sm text-ink-2">{t('page.loadFailed')}</p>

      {/*
        Перезавантаження сторінки звичайним посиланням не зробиш: браузер
        має піти на сервер по свіжий index.html, а не показати той, що в
        нього вже є. location.reload() робить саме це.
      */}
      <button type="button" onClick={() => location.reload()} className="btn btn-primary">
        {t('page.reload')}
      </button>
    </div>
  )
}
