import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { resetPassword } from '../api/auth'
import { ApiError } from '../api/client'
import { openSignIn } from '../auth/signInPrompt'

/**
 * Сторінка, на яку веде посилання з листа. Пошта й токен приходять у
 * параметрах адреси — людина їх не вводить і навіть не бачить.
 */
export function ResetPasswordPage() {
  const [params] = useSearchParams()
  const email = params.get('email') ?? ''
  const token = params.get('token') ?? ''

  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)

  const submit = useMutation({
    mutationFn: () => resetPassword(email, token, password),
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : 'Не вдалося змінити пароль.'),
  })

  // Посилання без параметрів — найімовірніше, поштова програма обрізала його.
  if (!email || !token) {
    return (
      <Notice>
        Посилання неповне. Скопіюйте його з листа повністю або{' '}
        <Link to="/" className="text-accent hover:underline">
          попросіть новий
        </Link>
        .
      </Notice>
    )
  }

  if (submit.isSuccess) {
    return (
      <Notice>
        <span className="font-semibold text-good">Пароль змінено.</span>
        <br />
        <button type="button" onClick={openSignIn} className="mt-2 text-accent hover:underline">
          Увійти з новим паролем
        </button>
      </Notice>
    )
  }

  return (
    <div className="wrap py-16">
      <form
        className="card mx-auto grid max-w-[420px] gap-3.5 p-6"
        onSubmit={(event) => {
          event.preventDefault()
          setError(null)
          submit.mutate()
        }}
      >
        <h1 className="font-display text-xl font-bold">Новий пароль</h1>
        <p className="text-[13px] text-ink-2">для {email}</p>

        <label className="flex flex-col gap-1.5">
          <span className="text-[11.5px] font-semibold text-ink-2">Пароль</span>
          <input
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            autoComplete="new-password"
            required
            className="control"
          />
          <span className="text-[12px] text-ink-3">
            Щонайменше 8 символів, велика й мала літери та цифра
          </span>
        </label>

        {error && (
          <p className="rounded-control bg-danger-soft px-3 py-2 text-[13px] text-danger">{error}</p>
        )}

        <button
          type="submit"
          disabled={submit.isPending || password.length < 8}
          className="btn btn-primary w-full py-2.5"
        >
          {submit.isPending ? 'Зберігаємо…' : 'Задати пароль'}
        </button>
      </form>
    </div>
  )
}

function Notice({ children }: { children: React.ReactNode }) {
  return (
    <div className="wrap py-16">
      <p className="card mx-auto max-w-[460px] p-10 text-center text-sm text-ink-2">{children}</p>
    </div>
  )
}
