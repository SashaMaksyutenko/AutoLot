import { createContext } from 'react'
import type { LoginRequest, RegisterRequest, UserProfile } from '../api/auth'

export interface AuthState {
  user: UserProfile | null

  /** Поки триває поновлення сесії при старті, не варто показувати «Увійти». */
  isRestoring: boolean

  login: (request: LoginRequest) => Promise<void>
  register: (request: RegisterRequest) => Promise<void>
  logout: () => Promise<void>

  /** Перечитує профіль із сервера — після того, як його змінили в кабінеті. */
  refreshProfile: () => Promise<void>
}

/**
 * Контекст винесено в окремий файл від компонента навмисно: інакше модуль
 * експортував би і компонент, і значення, а гаряче перезавантаження таке
 * поєднання не переживає — редагування хука перезапускало б усе дерево.
 */
export const AuthContext = createContext<AuthState | null>(null)
