import { getAccessToken, setAccessToken } from '../auth/tokenStore'

/**
 * Тонка обгортка над fetch. У розробці шлях відносний — його проксює Vite
 * на бекенд; у продакшені базу задає VITE_API_BASE_URL.
 */
const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''

export class ApiError extends Error {
  readonly status: number
  /** Помилки валідації за полями, як їх повертає ProblemDetails. */
  readonly errors: Record<string, string[]>

  constructor(message: string, status: number, errors: Record<string, string[]> = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.errors = errors
  }
}

interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>

  /**
   * Обмежувач частоти запитів відповідає власним форматом `{"error": …}`,
   * а не ProblemDetails. Без цього поля людина, яка десять разів помилилася
   * паролем, побачила б технічне «запит завершився помилкою» замість
   * зрозумілого «спробуйте трохи пізніше».
   */
  error?: string
}

/**
 * Поновлення сесії. Спроба одна на всіх: якщо три запити водночас отримали
 * 401, вони мають дочекатися одного поновлення, а не влаштувати три —
 * друге й третє все одно провалилися б, бо refresh-токен ротується.
 */
let refreshing: Promise<boolean> | null = null

async function refreshSession(): Promise<boolean> {
  refreshing ??= (async () => {
    try {
      const response = await fetch(`${baseUrl}/api/auth/refresh`, {
        method: 'POST',

        // Cookie з refresh-токеном httpOnly: JavaScript її не бачить,
        // але браузер надішле, якщо явно попросити.
        credentials: 'include',
      })

      if (!response.ok) {
        setAccessToken(null)
        return false
      }

      const payload = (await response.json()) as { accessToken: string }
      setAccessToken(payload.accessToken)

      return true
    } catch {
      setAccessToken(null)
      return false
    } finally {
      refreshing = null
    }
  })()

  return refreshing
}

async function send(path: string, init: RequestInit, retryOnUnauthorized: boolean): Promise<Response> {
  const token = getAccessToken()

  const response = await fetch(`${baseUrl}${path}`, {
    ...init,
    credentials: 'include',
    headers: {
      Accept: 'application/json',
      ...(init.body ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init.headers,
    },
  })

  // Токен живе 15 хвилин, тож 401 на робочій сесії — звичайна річ, а не
  // помилка користувача. Поновлюємо мовчки й повторюємо запит один раз.
  if (response.status === 401 && retryOnUnauthorized && (await refreshSession())) {
    return send(path, init, false)
  }

  return response
}

async function parse<T>(response: Response, path: string): Promise<T> {
  if (response.status === 204) {
    return undefined as T
  }

  const payload = (await response.json().catch(() => null)) as (T & ProblemDetails) | null

  if (!response.ok) {
    throw new ApiError(
      payload?.detail ?? payload?.error ?? payload?.title ?? `Запит ${path} завершився помилкою`,
      response.status,
      payload?.errors ?? {},
    )
  }

  if (payload === null) {
    throw new ApiError(`Некоректна відповідь від ${path}`, response.status)
  }

  return payload
}

export async function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  return parse<T>(await send(path, { signal }, true), path)
}

export async function apiPost<T>(path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  const init: RequestInit = { method: 'POST', signal }

  if (body !== undefined) {
    init.body = JSON.stringify(body)
  }

  return parse<T>(await send(path, init, true), path)
}

/**
 * PUT означає «зроби так, щоб стало отак». На відміну від POST його можна
 * повторювати скільки завгодно: додати оголошення в обране вдруге — це той
 * самий стан, а не другий запис.
 */
export async function apiPut<T>(path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  const init: RequestInit = { method: 'PUT', signal }

  if (body !== undefined) {
    init.body = JSON.stringify(body)
  }

  return parse<T>(await send(path, init, true), path)
}

export async function apiDelete<T>(path: string, signal?: AbortSignal): Promise<T> {
  return parse<T>(await send(path, { method: 'DELETE', signal }, true), path)
}

/** Поновлює сесію з cookie при старті застосунку. */
export { refreshSession }
