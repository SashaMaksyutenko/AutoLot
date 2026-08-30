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

/** Надіслати лист підтвердження ще раз — для того, хто вже увійшов. */
export function resendConfirmation(): Promise<void> {
  return apiPost<void>('/api/auth/resend-confirmation')
}
