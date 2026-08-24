/**
 * Access-токен живе **лише в пам'яті вкладки**, а не в localStorage.
 *
 * Токен у localStorage читається будь-яким скриптом на сторінці, тож одна
 * вразливість у сторонній бібліотеці віддає чужий доступ назавжди. У пам'яті
 * він зникає разом із вкладкою — а щоб не доводилося входити щоразу, сесію
 * поновлює refresh-cookie, яку JavaScript не бачить взагалі.
 *
 * Модуль навмисно простий і без React: ним користується і шар запитів, який
 * про React нічого не знає, і контекст автентифікації.
 */
let accessToken: string | null = null

const listeners = new Set<(token: string | null) => void>()

export function getAccessToken(): string | null {
  return accessToken
}

export function setAccessToken(token: string | null): void {
  accessToken = token

  for (const listener of listeners) {
    listener(token)
  }
}

/** Повертає функцію відписки — її викликає useEffect при розмонтуванні. */
export function subscribeToToken(listener: (token: string | null) => void): () => void {
  listeners.add(listener)

  return () => {
    listeners.delete(listener)
  }
}
