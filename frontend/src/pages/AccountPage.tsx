import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { resendConfirmation, updateProfile, type UserProfile } from '../api/auth'
import { ApiError } from '../api/client'
import { fetchMyDealerships } from '../api/dealership'
import { useAuth } from '../auth/useAuth'
import { openSignIn } from '../auth/signInPrompt'
import { Billing } from '../components/account/Billing'
import { MyListings } from '../components/account/MyListings'
import { MyPurchases } from '../components/account/MyPurchases'
import { MyReputation } from '../components/account/MyReputation'
import { VerifiedMark } from '../components/catalog/ListingCard'

/**
 * Кабінет: усе про себе в одному місці.
 *
 * Досі його не було зовсім — навіть щоб побачити власні ролі, доводилося
 * лізти в базу. Тут вони видно одразу, разом із профілем, місцезнаходженням
 * і салонами, де людина працює.
 */
export function AccountPage() {
  const auth = useAuth()

  if (auth.isRestoring) {
    return <Notice>Завантажуємо…</Notice>
  }

  if (!auth.user) {
    return (
      <Notice>
        Щоб побачити кабінет,{' '}
        <button type="button" onClick={openSignIn} className="text-accent hover:underline">
          увійдіть
        </button>
        .
      </Notice>
    )
  }

  return (
    <div className="wrap grid gap-4 py-[26px]">
      <h1 className="font-display text-[25px] font-bold">Кабінет</h1>

      <div className="grid items-start gap-4 lg:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)]">
        <div className="grid gap-4">
          <ProfileForm profile={auth.user} onSaved={auth.refreshProfile} />
          <Billing />
          <MyListings />
          <MyPurchases />
        </div>

        <div className="grid gap-4">
          <MyReputation userId={auth.user.id} />
          <AccessCard profile={auth.user} />
          <DealershipsCard />
        </div>
      </div>
    </div>
  )
}

function ProfileForm({
  profile,
  onSaved,
}: {
  profile: UserProfile
  onSaved: () => void | Promise<void>
}) {
  const [displayName, setDisplayName] = useState(profile.displayName)
  const [phoneNumber, setPhoneNumber] = useState(profile.phoneNumber ?? '')
  const [saved, setSaved] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  // Профіль міг оновитися ззовні — наприклад, після повторного входу.
  useEffect(() => {
    setDisplayName(profile.displayName)
    setPhoneNumber(profile.phoneNumber ?? '')
  }, [profile.displayName, profile.phoneNumber])

  const save = useMutation({
    mutationFn: () => updateProfile({ displayName, phoneNumber: phoneNumber || null }),
    onSuccess: async () => {
      setError(null)
      setFieldErrors({})
      setSaved(true)
      await onSaved()
    },
    onError: (caught) => {
      setSaved(false)

      if (caught instanceof ApiError) {
        setError(caught.message)
        setFieldErrors(caught.errors)
      } else {
        setError('Не вдалося зберегти.')
      }
    },
  })

  return (
    <section className="card grid gap-3.5 p-5">
      <h2 className="eyebrow">Профіль</h2>

      <form
        className="grid gap-3.5"
        onSubmit={(event) => {
          event.preventDefault()
          save.mutate()
        }}
      >
        <Field label="Як до вас звертатися" errors={fieldErrors.DisplayName}>
          <input
            value={displayName}
            onChange={(event) => {
              setDisplayName(event.target.value)
              setSaved(false)
            }}
            required
            className="control"
          />
        </Field>

        <Field
          label="Телефон"
          errors={fieldErrors.PhoneNumber}
          hint="У форматі +380XXXXXXXXX. Його побачать покупці на ваших оголошеннях."
        >
          <input
            value={phoneNumber}
            onChange={(event) => {
              setPhoneNumber(event.target.value)
              setSaved(false)
            }}
            placeholder="+380671234567"
            className="control font-mono"
          />
        </Field>

        {/* Пошту тут не міняють: це окремий сценарій із підтвердженням нової
            скриньки, інакше нею можна було б перехопити чужий акаунт. */}
        <Field label="Пошта">
          <input value={profile.email} disabled className="control" />
        </Field>

        {error && (
          <p className="rounded-control bg-danger-soft px-3 py-2 text-[13px] text-danger">{error}</p>
        )}

        {saved && <p className="text-[13px] text-good">Збережено.</p>}

        <button type="submit" disabled={save.isPending} className="btn btn-primary justify-self-start">
          {save.isPending ? 'Зберігаємо…' : 'Зберегти'}
        </button>
      </form>
    </section>
  )
}

/**
 * Хто ви на майданчику: підтверджена пошта й ролі.
 *
 * Ролі показуємо явно саме тому, що раніше перевірити їх можна було лише
 * запитом до бази.
 */
function AccessCard({ profile }: { profile: UserProfile }) {
  const [sent, setSent] = useState(false)

  const resend = useMutation({
    mutationFn: resendConfirmation,
    onSuccess: () => setSent(true),
  })

  const isStaff = profile.roles.some((role) => role === 'Moderator' || role === 'Admin')

  return (
    <section className="card grid gap-3 p-5">
      <h2 className="eyebrow">Доступ</h2>

      <div className="flex flex-wrap gap-1.5">
        {profile.roles.map((role) => (
          <span key={role} className={`pill ${role === 'User' ? '' : 'pill-accent'}`}>
            {roleLabel(role)}
          </span>
        ))}
        <span className="pill">
          {profile.accountType === 'Dealer' ? 'Акаунт салону' : 'Приватна особа'}
        </span>
      </div>

      {isStaff && (
        <Link to="/admin" className="btn justify-self-start">
          Перейти в адмінку
        </Link>
      )}

      <div className="border-t border-line pt-3">
        {profile.emailConfirmed ? (
          <p className="flex items-center gap-1.5 text-[13px] text-good">
            <VerifiedMark />
            Пошту підтверджено
          </p>
        ) : (
          <div className="grid gap-2">
            <p className="text-[13px] text-ink-2">
              Пошта не підтверджена — без цього ми не надсилаємо сповіщень про ставки.
            </p>

            {sent ? (
              <p className="text-[13px] text-good">Лист надіслано — перевірте скриньку.</p>
            ) : (
              <button
                type="button"
                onClick={() => resend.mutate()}
                disabled={resend.isPending}
                className="btn justify-self-start"
              >
                {resend.isPending ? 'Надсилаємо…' : 'Надіслати лист ще раз'}
              </button>
            )}
          </div>
        )}
      </div>
    </section>
  )
}

/** Салони, де людина працює. Порожній блок не показуємо взагалі. */
function DealershipsCard() {
  const dealerships = useQuery({
    queryKey: ['my-dealerships'],
    queryFn: ({ signal }) => fetchMyDealerships(signal),
  })

  if (!dealerships.data?.length) {
    return null
  }

  return (
    <section className="card grid gap-2 p-5">
      <h2 className="eyebrow">Мої салони</h2>

      {dealerships.data.map((membership) => (
        <div key={membership.dealershipId} className="flex items-center justify-between gap-3">
          <Link
            to={`/dealers/${membership.slug}`}
            className="flex items-center gap-1.5 text-[14px] font-semibold hover:text-accent"
          >
            {membership.isVerified && <VerifiedMark />}
            {membership.name}
          </Link>
          <span className="pill">
            {membership.role === 'Owner' ? 'Власник' : 'Менеджер'}
          </span>
        </div>
      ))}
    </section>
  )
}

function roleLabel(role: string): string {
  if (role === 'Admin') return 'Адміністратор'
  if (role === 'Moderator') return 'Модератор'

  return 'Користувач'
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
      {hint && !errors && <span className="text-[12px] text-ink-3">{hint}</span>}
      {errors?.map((message) => (
        <span key={message} className="text-[12px] text-danger">
          {message}
        </span>
      ))}
    </label>
  )
}

function Notice({ children }: { children: React.ReactNode }) {
  return (
    <div className="wrap py-16">
      <p className="card mx-auto max-w-[460px] p-10 text-center text-sm text-ink-2">{children}</p>
    </div>
  )
}
