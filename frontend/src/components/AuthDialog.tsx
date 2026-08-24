import { useState } from 'react'
import { ApiError } from '../api/client'
import type { AccountType } from '../api/auth'
import { useAuth } from '../auth/useAuth'

type Mode = 'login' | 'register'

/**
 * Вікно входу й реєстрації. Обидві форми живуть тут разом, бо різняться
 * лише кількома полями, а перемикатися між ними людина може посеред вводу.
 */
export function AuthDialog({ onClose }: { onClose: () => void }) {
  const auth = useAuth()

  const [mode, setMode] = useState<Mode>('login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [accountType, setAccountType] = useState<AccountType>('Private')

  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  async function submit(event: React.FormEvent) {
    event.preventDefault()

    setBusy(true)
    setError(null)
    setFieldErrors({})

    try {
      if (mode === 'login') {
        await auth.login({ email, password })
      } else {
        await auth.register({ email, password, displayName, accountType })
      }

      onClose()
    } catch (caught) {
      if (caught instanceof ApiError) {
        setError(caught.message)
        setFieldErrors(caught.errors)
      } else {
        setError('Не вдалося зв’язатися з сервером.')
      }
    } finally {
      setBusy(false)
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
      onClick={onClose}
    >
      <div
        className="w-full max-w-[420px] rounded-md border border-line bg-surface p-6"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="mb-5 flex gap-1 rounded-sm bg-[#e4e8ec] p-0.5">
          <Tab active={mode === 'login'} onClick={() => setMode('login')} label="Вхід" />
          <Tab active={mode === 'register'} onClick={() => setMode('register')} label="Реєстрація" />
        </div>

        <form onSubmit={submit} className="flex flex-col gap-3.5">
          {mode === 'register' && (
            <Field label="Як до вас звертатися" errors={fieldErrors.DisplayName}>
              <input
                value={displayName}
                onChange={(event) => setDisplayName(event.target.value)}
                autoComplete="name"
                required
                className={inputClass}
              />
            </Field>
          )}

          <Field label="Email" errors={fieldErrors.Email}>
            <input
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              autoComplete="email"
              required
              className={inputClass}
            />
          </Field>

          <Field
            label="Пароль"
            errors={fieldErrors.Password}
            hint={mode === 'register' ? 'Щонайменше 8 символів, велика й мала літери та цифра' : undefined}
          >
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
              required
              className={inputClass}
            />
          </Field>

          {mode === 'register' && (
            <Field label="Тип акаунта">
              <div className="flex gap-2">
                <AccountChoice
                  label="Приватна особа"
                  active={accountType === 'Private'}
                  onClick={() => setAccountType('Private')}
                />
                <AccountChoice
                  label="Автосалон"
                  active={accountType === 'Dealer'}
                  onClick={() => setAccountType('Dealer')}
                />
              </div>
            </Field>
          )}

          {error && (
            <p className="rounded-sm bg-warn-soft px-3 py-2 text-[13px] text-warn-ink">{error}</p>
          )}

          <button
            type="submit"
            disabled={busy}
            className="mt-1 h-11 rounded-sm bg-brand text-sm font-semibold text-white disabled:opacity-60"
          >
            {busy ? 'Хвилинку…' : mode === 'login' ? 'Увійти' : 'Зареєструватися'}
          </button>
        </form>
      </div>
    </div>
  )
}

const inputClass =
  'h-10 w-full rounded-sm border border-line-strong bg-surface px-3 text-sm outline-none focus:border-brand'

function Tab({ active, onClick, label }: { active: boolean; onClick: () => void; label: string }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`flex-grow rounded-[3px] py-2 text-sm ${
        active ? 'bg-surface font-semibold text-ink' : 'text-muted'
      }`}
    >
      {label}
    </button>
  )
}

function Field({
  label,
  hint,
  errors,
  children,
}: {
  label: string
  hint?: string
  errors?: string[]
  children: React.ReactNode
}) {
  return (
    <label className="flex flex-col gap-1.5">
      <span className="text-[13px] font-medium text-muted">{label}</span>
      {children}
      {hint && !errors && <span className="text-[12px] text-faint">{hint}</span>}
      {errors?.map((message) => (
        <span key={message} className="text-[12px] text-warn-ink">
          {message}
        </span>
      ))}
    </label>
  )
}

function AccountChoice({
  label,
  active,
  onClick,
}: {
  label: string
  active: boolean
  onClick: () => void
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`flex-grow rounded-sm border py-2 text-[13px] ${
        active ? 'border-brand bg-brand-soft font-semibold text-brand' : 'border-line-strong text-muted'
      }`}
    >
      {label}
    </button>
  )
}
