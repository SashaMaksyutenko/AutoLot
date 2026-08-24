import { apiGet, apiPost } from './client'
import type { UserLocation } from './listing'

export type AccountType = 'Private' | 'Dealer'

export interface UserProfile {
  id: number
  email: string
  displayName: string
  accountType: AccountType
  emailConfirmed: boolean
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
