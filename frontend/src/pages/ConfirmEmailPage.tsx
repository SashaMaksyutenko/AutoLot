import { useEffect, useRef } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { confirmEmail } from '../api/auth'

/**
 * Підтвердження пошти за посиланням із листа. Людині тут нічого робити —
 * сторінка сама надсилає токен і показує, що вийшло.
 */
export function ConfirmEmailPage() {
  const [params] = useSearchParams()
  const email = params.get('email') ?? ''
  const token = params.get('token') ?? ''

  const confirm = useMutation({ mutationFn: () => confirmEmail(email, token) })

  /*
    Надсилаємо один раз. Прапорець потрібен через режим розробки React: він
    навмисне монтує компоненти двічі, щоб виловити помилки, і без цього
    запит пішов би двічі.
  */
  const sent = useRef(false)

  useEffect(() => {
    if (sent.current || !email || !token) return

    sent.current = true
    confirm.mutate()
  }, [email, token, confirm])

  if (!email || !token) {
    return <Notice>Посилання неповне. Скопіюйте його з листа повністю.</Notice>
  }

  if (confirm.isPending) {
    return <Notice>Підтверджуємо…</Notice>
  }

  if (confirm.isError) {
    return (
      <Notice>
        Термін дії посилання минув або воно пошкоджене. Увійдіть і попросіть новий лист.
      </Notice>
    )
  }

  return (
    <Notice>
      <span className="font-semibold text-good">Пошту підтверджено.</span>
      <br />
      Тепер ми зможемо надсилати вам сповіщення про ставки й відповіді продавців.
      <br />
      <Link to="/" className="mt-2 inline-block text-accent hover:underline">
        До каталогу
      </Link>
    </Notice>
  )
}

function Notice({ children }: { children: React.ReactNode }) {
  return (
    <div className="wrap py-16">
      <p className="card mx-auto max-w-[460px] p-10 text-center text-sm text-ink-2">{children}</p>
    </div>
  )
}
