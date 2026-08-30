import { useCallback, useEffect, useMemo, useState } from 'react'
import { closeChat } from '../api/chatHub'
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

      // Канал листування треба розірвати саме тут: він тримає з'єднання
      // зі старим токеном, і наступний користувач у цьому ж браузері
      // отримував би чужі повідомлення.
      await closeChat()
    }
  }, [])

  /**
   * Перечитує профіль із сервера. Потрібен після змін у кабінеті: шапка й
   * решта сторінок беруть ім'я саме звідси, і без цього вони показували б
   * старе, доки людина не перезайде.
   *
   * Помилку ковтаємо навмисно: профіль уже збережено на сервері, а невдале
   * перечитування — не привід лякати людину повідомленням.
   */
  const refreshProfile = useCallback(async () => {
    try {
      setUser(await fetchProfile());
    } catch {
      // Лишаємо те, що вже показано.
    }
  }, []);

  const value = useMemo<AuthState>(
    () => ({ user, isRestoring, login, register, logout, refreshProfile }),
    [user, isRestoring, login, register, logout, refreshProfile],
  );

  return <AuthContext value={value}>{children}</AuthContext>
}
