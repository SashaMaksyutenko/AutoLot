import { use } from 'react'
import { AuthContext, type AuthState } from './authContext'

export function useAuth(): AuthState {
  const context = use(AuthContext)

  if (context === null) {
    throw new Error('useAuth використано поза AuthProvider')
  }

  return context
}
