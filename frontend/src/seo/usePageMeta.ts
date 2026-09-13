import { useEffect } from 'react'

/**
 * Заголовок вкладки й опис сторінки.
 *
 * Що це дає, а що ні. Заголовок працює завжди: він у вкладці, в історії й у
 * закладках. Опис читає Google — він виконує JavaScript, тож бачить те, що
 * ми проставили.
 *
 * А от месенджери JavaScript НЕ виконують. Картку з фото й ціною при
 * пересиланні посилання цей гачок не зробить — її складає сервер, коли
 * роздає зібраний сайт (див. SpaHosting на бекенді). Тут же — те, що
 * стосується самого браузера.
 */
export function usePageMeta(title: string, description?: string): void {
  useEffect(() => {
    // Порожній заголовок лишає той, що в оболонці, — це краще за порожню
    // вкладку, поки сторінка ще вантажиться.
    if (title.trim().length > 0) {
      document.title = `${title} · AutoLot`
    }

    if (description === undefined) return

    setMeta('description', description)
  }, [title, description])
}

/**
 * Ставить або оновлює тег. Створюємо його на льоту, бо в оболонці опису
 * немає: він у кожної сторінки свій, і тримати там заглушку означало б
 * показувати її всюди, де гачок не викликали.
 */
function setMeta(name: string, content: string): void {
  let tag = document.head.querySelector<HTMLMetaElement>(`meta[name="${name}"]`)

  if (!tag) {
    tag = document.createElement('meta')
    tag.name = name
    document.head.appendChild(tag)
  }

  tag.content = content
}
