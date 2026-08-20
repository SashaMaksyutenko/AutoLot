/**
 * Тонка обгортка над fetch. У розробці шлях відносний — його проксює Vite
 * на бекенд; у продакшені базу задає VITE_API_BASE_URL.
 */
const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''

export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

export async function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, {
    signal,
    headers: { Accept: 'application/json' },
  })

  // Health-check свідомо віддає 503 разом із тілом, тож розбираємо його теж.
  const payload = (await response.json().catch(() => null)) as T | null

  if (payload === null) {
    throw new ApiError(`Некоректна відповідь від ${path}`, response.status)
  }

  return payload
}
