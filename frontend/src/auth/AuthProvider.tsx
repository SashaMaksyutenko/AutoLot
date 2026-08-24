import { useCallback, useEffect, useMemo, useState } from 'react'
import { refreshSession } from '../api/client'
import {
  fetchProfile,
  login as loginRequest,
  logout as logoutRequest,
  register as registerRequest,
  type LoginRequest,
  type RegisterRequest,
  type UserProfile,
} from '../api/auth'
import { AuthContext, type AuthState } from './authContext'
import { setAccessToken, subscribeToToken } from './tokenStore'


export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<UserProfile | null>(null)
  const [isRestoring, setRestoring] = useState(true)

  // Сторінку могли перезавантажити: токен у пам'яті зник, але refresh-cookie
  // лишилася. Пробуємо поновити сесію мовчки — невдача тут не помилка,
  // це просто означає «гість».
  useEffect(() => {
    let cancelled = false

    async function restore() {
      try {
        if (await refreshSession()) {
          const profile = await fetchProfile()

          if (!cancelled) {
            setUser(profile)
          }
        }
      } finally {
        if (!cancelled) {
          setRestoring(false)
        }
      }
    }

    void restore()

    return () => {
      cancelled = true
    }
  }, [])

  // Якщо шар запитів скинув токен — наприклад, поновлення провалилося, —
  // інтерфейс має одразу перестати вважати користувача залогіненим.
  useEffect(() => subscribeToToken((token) => {
    if (token === null) {
      setUser(null)
    }
  }), [])

  const login = useCallback(async (request: LoginRequest) => {
    const response = await loginRequest(request)
    setAccessToken(response.accessToken)
    setUser(response.profile)
  }, [])

  const register = useCallback(async (request: RegisterRequest) => {
    const response = await registerRequest(request)
    setAccessToken(response.accessToken)
    setUser(response.profile)
  }, [])

  const logout = useCallback(async () => {
    try {
      // Гасимо сесію на сервері, щоб refresh-токен не лишився дійсним.
      await logoutRequest()
    } finally {
      setAccessToken(null)
      setUser(null)
    }
  }, [])

  const value = useMemo<AuthState>(
    () => ({ user, isRestoring, login, register, logout }),
    [user, isRestoring, login, register, logout],
  )

  return <AuthContext value={value}>{children}</AuthContext>
}
