import { apiGet, apiPost, apiPut } from './client'
import type { UserLocation } from './listing'

export type AccountType = 'Private' | 'Dealer'

export interface UserProfile {
  id: number
  email: string
  displayName: string
  accountType: AccountType
  emailConfirmed: boolean

  /** Порожній, поки не вказали. Гість чужого номера не бачить узагалі. */
  phoneNumber: string | null
  phoneNumberConfirmed: boolean
  roles: string[]
  location: UserLocation | null
}

/**
 * Відповідь на вхід чи реєстрацію. Refresh-токена тут немає навмисно: він
 * приходить окремою httpOnly cookie, якої JavaScript не бачить.
 */
export interface AuthResponse {
  accessToken: string
  expiresAt: string
  profile: UserProfile
}

export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest {
  email: string
  password: string
  displayName: string
  accountType: AccountType
  phoneNumber?: string
}

export function login(request: LoginRequest): Promise<AuthResponse> {
  return apiPost<AuthResponse>('/api/auth/login', request)
}

export function register(request: RegisterRequest): Promise<AuthResponse> {
  return apiPost<AuthResponse>('/api/auth/register', request)
}

export function logout(): Promise<void> {
  return apiPost<void>('/api/auth/logout')
}

export function fetchProfile(signal?: AbortSignal): Promise<UserProfile> {
  return apiGet<UserProfile>('/api/auth/me', signal)
}

/**
 * Відновлення пароля й підтвердження пошти.
 *
 * Прохання про лист завжди завершується успіхом — навіть для незареєстрованої
 * адреси. Це не помилка: інакше форма «забув пароль» перетворилася б на
 * спосіб перевіряти, хто є на майданчику.
 */

export function requestPasswordReset(email: string): Promise<void> {
  return apiPost<void>('/api/auth/forgot-password', { email })
}

export function resetPassword(
  email: string,
  token: string,
  newPassword: string,
): Promise<void> {
  return apiPost<void>('/api/auth/reset-password', { email, token, newPassword })
}

export function confirmEmail(email: string, token: string): Promise<void> {
  return apiPost<void>('/api/auth/confirm-email', { email, token })
}

/** Зміна імені й телефону. Пошта тут не міняється — це окремий сценарій. */
export function updateProfile(request: {
  displayName: string
  phoneNumber: string | null
}): Promise<UserProfile> {
  return apiPut<UserProfile>('/api/profile', request)
}

/**
 * Місцезнаходження. Окремим викликом, а не полем у updateProfile: сервер
 * перевіряє тут інше — що місто існує й що район належить саме йому.
 *
 * Порожнє місто означає «прибрати» — тоді разом із ним обнуляється й район,
 * бо район без міста ні про що не каже.
 */
export function updateLocation(request: {
  cityId: number | null
  cityDistrictId: number | null
}): Promise<UserProfile> {
  return apiPut<UserProfile>('/api/profile/location', request)
}

/** Які способи входу підняті на сервері. */
export interface AuthProviders {
  google: boolean
}

export function fetchAuthProviders(signal?: AbortSignal): Promise<AuthProviders> {
  return apiGet<AuthProviders>('/api/auth/providers', signal)
}

/**
 * Адреса, куди веде кнопка «Увійти через Google».
 *
 * Це НЕ запит через fetch, а справжній перехід усього вікна: браузер має
 * опинитися на сторінці згоди Google, а звідти повернутися назад. Зробити
 * це запитом неможливо — чужий домен не пустить.
 *
 * returnUrl кажемо абсолютним і беремо з поточної адреси, щоб людина
 * повернулася рівно туди, звідки пішла. Сервер його перевіряє за білим
 * списком origin: підсунути чужий сайт не вийде.
 */
export function googleSignInUrl(): string {
  const back = encodeURIComponent(window.location.href)

  return `/api/auth/google/start?returnUrl=${back}`
}

/** Надіслати лист підтвердження ще раз — для того, хто вже увійшов. */
export function resendConfirmation(): Promise<void> {
  return apiPost<void>('/api/auth/resend-confirmation')
}
