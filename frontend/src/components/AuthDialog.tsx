import { useState } from 'react'
import { ApiError } from '../api/client'
import { requestPasswordReset, type AccountType } from '../api/auth'
import { useAuth } from '../auth/useAuth'
import { useTranslation } from '../i18n/useTranslation'

type Mode = 'login' | 'register' | 'forgot'

/**
 * Вікно входу й реєстрації. Обидві форми живуть тут разом, бо різняться
 * лише кількома полями, а перемикатися між ними людина може посеред вводу.
 */
export function AuthDialog({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
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
        setError(t('error.network'))
      }
    } finally {
      setBusy(false)
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      onClick={onClose}
    >
      {/*
        stopPropagation зупиняє «спливання» кліку: без нього натискання
        всередині вікна дійшло б до підкладки й одразу його закрило.
      */}
      <div
        className="card w-full max-w-[420px] p-6"
        onClick={(event) => event.stopPropagation()}
      >
        {/* У режимі відновлення вкладки ховаємо: там інша задача. */}
        {mode !== 'forgot' && (
          <div className="mb-5 flex gap-0.5 rounded-control border border-line bg-surface-2 p-0.5">
            <Tab active={mode === 'login'} onClick={() => setMode('login')} label={t('auth.tabSignIn')} />
            <Tab active={mode === 'register'} onClick={() => setMode('register')} label={t('auth.tabRegister')} />
          </div>
        )}

        {mode === 'forgot' ? (
          <ForgotPassword onBack={() => setMode('login')} />
        ) : (
        <form onSubmit={submit} className="flex flex-col gap-3.5">
          {mode === 'register' && (
            <Field label={t('auth.displayName')} errors={fieldErrors.DisplayName}>
              <input
                value={displayName}
                onChange={(event) => setDisplayName(event.target.value)}
                autoComplete="name"
                required
                className="control"
              />
            </Field>
          )}

          <Field label={t('auth.email')} errors={fieldErrors.Email}>
            <input
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              autoComplete="email"
              required
              className="control"
            />
          </Field>

          <Field
            label={t('auth.password')}
            errors={fieldErrors.Password}
            hint={
              mode === 'register'
                ? t('auth.passwordHint')
                : undefined
            }
          >
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
              required
              className="control"
            />
          </Field>

          {mode === 'register' && (
            <Field label={t('auth.accountType')}>
              <div className="flex gap-2">
                <AccountChoice
                  label={t('auth.private')}
                  active={accountType === 'Private'}
                  onClick={() => setAccountType('Private')}
                />
                <AccountChoice
                  label={t('auth.dealer')}
                  active={accountType === 'Dealer'}
                  onClick={() => setAccountType('Dealer')}
                />
              </div>
            </Field>
          )}

          {error && (
            <p className="rounded-control bg-danger-soft px-3 py-2 text-[13px] text-danger">
              {error}
            </p>
          )}

          <button type="submit" disabled={busy} className="btn btn-primary mt-1 w-full py-2.5">
            {busy ? t('auth.busy') : mode === 'login' ? t('auth.signIn') : t('auth.register')}
          </button>

          {mode === 'login' && (
            <button
              type="button"
              onClick={() => setMode('forgot')}
              className="text-[13px] text-ink-2 hover:text-accent"
            >
              {t('auth.forgot')}
            </button>
          )}
        </form>
        )}
      </div>
    </div>
  )
}

/**
 * Прохання надіслати лист для відновлення пароля.
 *
 * Успіх показуємо однаково для будь-якої адреси — і зареєстрованої, і ні.
 * Так вирішив бекенд, і фронтенд не має права бути відвертішим: інакше
 * форма перетворилася б на спосіб перевіряти, хто є на майданчику.
 */
function ForgotPassword({ onBack }: { onBack: () => void }) {
  const { t } = useTranslation()
  const [email, setEmail] = useState('')
  const [sent, setSent] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  if (sent) {
    return (
      <div className="grid gap-3 text-center">
        <h2 className="font-display text-lg font-bold">{t('auth.checkMail')}</h2>
        <p className="text-[13px] text-ink-2">{t('auth.checkMailText', { email })}</p>
        <button type="button" onClick={onBack} className="btn w-full">
          {t('auth.backToSignIn')}
        </button>
      </div>
    )
  }

  return (
    <form
      className="grid gap-3.5"
      onSubmit={async (event) => {
        event.preventDefault()
        setBusy(true)
        setError(null)

        try {
          await requestPasswordReset(email)
          setSent(true)
        } catch (caught) {
          setError(
            caught instanceof ApiError ? caught.message : t('error.network'),
          )
        } finally {
          setBusy(false)
        }
      }}
    >
      <h2 className="font-display text-lg font-bold">{t('auth.resetTitle')}</h2>
      <p className="text-[13px] text-ink-2">{t('auth.resetLead')}</p>

      <Field label={t('auth.email')}>
        <input
          type="email"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          autoComplete="email"
          required
          className="control"
        />
      </Field>

      {error && (
        <p className="rounded-control bg-danger-soft px-3 py-2 text-[13px] text-danger">{error}</p>
      )}

      <button type="submit" disabled={busy} className="btn btn-primary w-full py-2.5">
        {busy ? t('auth.resetSending') : t('auth.resetSend')}
      </button>

      <button type="button" onClick={onBack} className="text-[13px] text-ink-2 hover:text-accent">
        {t('auth.resetRemembered')}
      </button>
    </form>
  )
}

function Tab({ active, onClick, label }: { active: boolean; onClick: () => void; label: string }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      className={`flex-1 rounded-[4px] py-1.5 text-sm ${
        active ? 'bg-surface font-semibold text-ink' : 'text-ink-2'
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
      <span className="text-[11.5px] font-semibold text-ink-2">{label}</span>
      {children}
      {/* Підказку ховаємо, щойно з'явилася помилка: два написи поспіль зайві. */}
      {hint && !errors && <span className="text-[12px] text-ink-3">{hint}</span>}
      {errors?.map((message) => (
        <span key={message} className="text-[12px] text-danger">
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
      aria-pressed={active}
      className={`flex-1 rounded-control border py-2 text-[13px] ${
        active
          ? 'border-accent bg-accent-soft font-semibold text-accent'
          : 'border-line text-ink-2 hover:border-ink-3'
      }`}
    >
      {label}
    </button>
  )
}
