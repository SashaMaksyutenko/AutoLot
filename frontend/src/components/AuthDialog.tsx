import { useState } from 'react'
import { ApiError } from '../api/client'
import {
  fetchAuthProviders,
  googleSignInUrl,
  requestPasswordReset,
  type AccountType,
} from '../api/auth'
import { useQuery } from '@tanstack/react-query'
import { useSignInReason } from '../auth/signInPrompt'
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

  // Причина, з якою вікно відкрилося саме по собі: людина щойно
  // повернулася від Google і має дізнатися, чому нічого не вийшло.
  const reason = useSignInReason()

  const providers = useQuery({
    queryKey: ['auth-providers'],
    queryFn: ({ signal }) => fetchAuthProviders(signal),

    // Набір способів входу не змінюється без перезапуску сервера.
    staleTime: Infinity,
  })

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
        {/*
          Google показуємо лише коли сервер каже, що ключі на місці:
          без них кнопка вела б у помилку 501.
        */}
        {mode !== 'forgot' && providers.data?.google && (
          <div className="mb-4 grid gap-3">
            <a href={googleSignInUrl()} className="btn w-full justify-center py-2.5">
              <GoogleMark />
              {t('auth.google')}
            </a>

            {/* Риска з написом посередині: дві лінії, що ростуть обабіч. */}
            <div className="flex items-center gap-3 text-[12px] text-ink-3">
              <span className="h-px flex-1 bg-line" />
              {t('auth.or')}
              <span className="h-px flex-1 bg-line" />
            </div>
          </div>
        )}

        {/* У режимі відновлення вкладки ховаємо: там інша задача. */}
        {mode !== 'forgot' && (
          <div className="mb-5 flex gap-0.5 rounded-control border border-line bg-surface-2 p-0.5">
            <Tab active={mode === 'login'} onClick={() => setMode('login')} label={t('auth.tabSignIn')} />
            <Tab active={mode === 'register'} onClick={() => setMode('register')} label={t('auth.tabRegister')} />
          </div>
        )}

        {reason && (
          <p className="mb-3 rounded-control bg-danger-soft px-3 py-2 text-[13px] text-danger">
            {reason}
          </p>
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

/*
  Кольоровий значок Google. Намальований у коді, а не взятий файлом: одна
  дрібна картинка не варта ані окремого запиту, ані сторонньої бібліотеки —
  так само, як піктограми теми.

  Кольори тут фіксовані навмисно: це чужий знак, і він має виглядати
  однаково в обох темах.
*/
function GoogleMark() {
  return (
    <svg width="16" height="16" viewBox="0 0 48 48" aria-hidden="true" className="shrink-0">
      <path
        fill="#4285F4"
        d="M45.1 24.5c0-1.6-.1-3.1-.4-4.5H24v8.6h11.8a10 10 0 0 1-4.4 6.6v5.5h7.1c4.2-3.8 6.6-9.5 6.6-16.2z"
      />
      <path
        fill="#34A853"
        d="M24 46c6 0 11-2 14.5-5.3l-7.1-5.5c-2 1.3-4.5 2.1-7.4 2.1-5.7 0-10.5-3.8-12.2-9H4.5v5.7A22 22 0 0 0 24 46z"
      />
      <path fill="#FBBC05" d="M11.8 28.3a13.2 13.2 0 0 1 0-8.6v-5.7H4.5a22 22 0 0 0 0 20l7.3-5.7z" />
      <path
        fill="#EA4335"
        d="M24 9.5c3.2 0 6.1 1.1 8.4 3.3l6.3-6.3A22 22 0 0 0 4.5 14l7.3 5.7c1.7-5.2 6.5-9 12.2-9z"
      />
    </svg>
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
