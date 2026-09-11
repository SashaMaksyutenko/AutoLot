import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import {
  resendConfirmation,
  updateLocation,
  updateProfile,
  type UserProfile,
} from '../api/auth'
import { ApiError } from '../api/client'
import { fetchCities, fetchCityDistricts, fetchRegions } from '../api/reference'
import { fetchMyDealerships } from '../api/dealership'
import { useAuth } from '../auth/useAuth'
import { openSignIn } from '../auth/signInPrompt'
import { useTranslation } from '../i18n/useTranslation'
import type { MessageKey } from '../i18n/messages'
import { Billing } from '../components/account/Billing'
import { MyListings } from '../components/account/MyListings'
import { MyPurchases } from '../components/account/MyPurchases'
import { MyReputation } from '../components/account/MyReputation'
import { TelegramCard } from '../components/account/TelegramCard'
import { VerifiedMark } from '../components/catalog/ListingCard'

/**
 * Кабінет: усе про себе в одному місці.
 *
 * Досі його не було зовсім — навіть щоб побачити власні ролі, доводилося
 * лізти в базу. Тут вони видно одразу, разом із профілем, місцезнаходженням
 * і салонами, де людина працює.
 */
export function AccountPage() {
  const { t } = useTranslation()

  const auth = useAuth()

  if (auth.isRestoring) {
    return <Notice>{t('cabinet.loading')}</Notice>
  }

  if (!auth.user) {
    return (
      <Notice>
        {t('cabinet.signInLead')}{' '}
        <button type="button" onClick={openSignIn} className="text-accent hover:underline">
          {t('cabinet.signIn')}
        </button>
        .
      </Notice>
    )
  }

  return (
    <div className="wrap grid gap-4 py-[26px]">
      <h1 className="font-display text-[25px] font-bold">{t('cabinet.title')}</h1>

      <div className="grid items-start gap-4 lg:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)]">
        <div className="grid gap-4">
          <ProfileForm profile={auth.user} onSaved={auth.refreshProfile} />
          <LocationForm profile={auth.user} onSaved={auth.refreshProfile} />
          <Billing />
          <MyListings />
          <MyPurchases />
        </div>

        <div className="grid gap-4">
          <MyReputation userId={auth.user.id} />
          <AccessCard profile={auth.user} />
          <TelegramCard />
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
  const { t } = useTranslation()

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
        setError(t('cabinet.saveFailed'))
      }
    },
  })

  return (
    <section className="card grid gap-3.5 p-5">
      <h2 className="eyebrow">{t('cabinet.profile')}</h2>

      <form
        className="grid gap-3.5"
        onSubmit={(event) => {
          event.preventDefault()
          save.mutate()
        }}
      >
        <Field label={t('cabinet.displayName')} errors={fieldErrors.DisplayName}>
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
          label={t('cabinet.phone')}
          errors={fieldErrors.PhoneNumber}
          hint={t('cabinet.phoneHint')}
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
        <Field label={t('cabinet.email')}>
          <input value={profile.email} disabled className="control" />
        </Field>

        {error && (
          <p className="rounded-control bg-danger-soft px-3 py-2 text-[13px] text-danger">{error}</p>
        )}

        {saved && <p className="text-[13px] text-good">{t('cabinet.saved')}</p>}

        <button type="submit" disabled={save.isPending} className="btn btn-primary justify-self-start">
          {save.isPending ? t('cabinet.saving') : t('cabinet.save')}
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
/**
 * Місто й район.
 *
 * Окремою карткою від імені й телефону, бо це окремий запит до сервера з
 * власними правилами перевірки. Тримати їх в одній формі означало б, що
 * помилка в місті скасовує збережене ім'я.
 */
function LocationForm({
  profile,
  onSaved,
}: {
  profile: UserProfile
  onSaved: () => Promise<void> | void
}) {
  const { t } = useTranslation()

  // Область у профілі не зберігається — місто знає її саме. Але вибір
  // двоступеневий, тож при відкритті беремо її з наявного місця.
  const [regionId, setRegionId] = useState<number | undefined>(profile.location?.regionId)
  const [cityId, setCityId] = useState<number | null>(profile.location?.cityId ?? null)
  const [districtId, setDistrictId] = useState<number | null>(
    profile.location?.cityDistrictId ?? null,
  )

  const [saved, setSaved] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const regions = useQuery({
    queryKey: ['regions'],
    queryFn: ({ signal }) => fetchRegions(signal),
    staleTime: Infinity,
  })

  const cities = useQuery({
    queryKey: ['cities', regionId],
    queryFn: ({ signal }) => fetchCities(regionId!, signal),
    enabled: regionId !== undefined,
    staleTime: Infinity,
  })

  const districts = useQuery({
    queryKey: ['city-districts', cityId],
    queryFn: ({ signal }) => fetchCityDistricts(cityId!, signal),
    enabled: cityId !== null,
    staleTime: Infinity,
  })

  /*
    Що зберігати, мутація отримує АРГУМЕНТОМ, а не читає зі стану.

    Різниця не косметична. setState не змінює значення миттєво — до кінця
    поточного оброблення воно старе. Кнопка «прибрати» робить setCityId(null)
    і одразу викликає збереження; якби mutationFn читала стан, вона взяла б
    ще не скинуте місто й зберегла те саме, що й було.
  */
  const save = useMutation({
    mutationFn: (place: { cityId: number | null; cityDistrictId: number | null }) =>
      updateLocation(place),
    onSuccess: async () => {
      setError(null)
      setSaved(true)
      await onSaved()
    },
    onError: (caught) => {
      setSaved(false)
      setError(caught instanceof ApiError ? caught.message : t('place.failed'))
    },
  })

  return (
    <form
      className="card grid gap-3 p-5"
      onSubmit={(event) => {
        event.preventDefault()
        save.mutate({ cityId, cityDistrictId: districtId })
      }}
    >
      <div>
        <h2 className="eyebrow">{t('place.title')}</h2>
        <p className="mt-1 text-[12.5px] text-ink-3">{t('place.lead')}</p>
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t('filter.region')}>
          <select
            value={regionId ?? ''}
            // Місто належить області, район — місту: зміна старшого рівня
            // скидає обидва молодші.
            onChange={(event) => {
              setRegionId(event.target.value ? Number(event.target.value) : undefined)
              setCityId(null)
              setDistrictId(null)
              setSaved(false)
            }}
            className="control"
          >
            <option value="">{t('place.none')}</option>
            {regions.data?.map((region) => (
              <option key={region.id} value={region.id}>
                {region.name}
              </option>
            ))}
          </select>
        </Field>

        <Field label={t('form.city')}>
          <select
            value={cityId ?? ''}
            disabled={regionId === undefined}
            onChange={(event) => {
              setCityId(event.target.value ? Number(event.target.value) : null)
              setDistrictId(null)
              setSaved(false)
            }}
            className="control"
          >
            <option value="">
              {regionId ? t('filter.allCities') : t('filter.chooseRegionFirst')}
            </option>
            {cities.data?.map((city) => (
              <option key={city.id} value={city.id}>
                {city.name}
              </option>
            ))}
          </select>
        </Field>

        {/* Райони є лише у великих містах, тож і вибір показуємо лише там. */}
        {(districts.data?.length ?? 0) > 0 && (
          <Field label={t('form.district')}>
            <select
              value={districtId ?? ''}
              onChange={(event) => {
                setDistrictId(event.target.value ? Number(event.target.value) : null)
                setSaved(false)
              }}
              className="control"
            >
              <option value="">{t('filter.allDistricts')}</option>
              {districts.data?.map((district) => (
                <option key={district.id} value={district.id}>
                  {district.name}
                </option>
              ))}
            </select>
          </Field>
        )}
      </div>

      {error && (
        <p className="rounded-control bg-danger-soft px-3 py-2 text-[13px] text-danger">{error}</p>
      )}

      {saved && <p className="text-[13px] text-good">{t('place.saved')}</p>}

      <div className="flex flex-wrap gap-2">
        <button type="submit" disabled={save.isPending} className="btn btn-primary justify-self-start">
          {save.isPending ? t('cabinet.saving') : t('cabinet.save')}
        </button>

        {/* Прибрати місто — окрема дія: порожній вибір і «зберегти» поспіль
            читалися б як «нічого не робити». */}
        {profile.location && (
          <button
            type="button"
            disabled={save.isPending}
            onClick={() => {
              setRegionId(undefined)
              setCityId(null)
              setDistrictId(null)
              save.mutate({ cityId: null, cityDistrictId: null })
            }}
            className="btn"
          >
            {t('place.clear')}
          </button>
        )}
      </div>
    </form>
  )
}

function AccessCard({ profile }: { profile: UserProfile }) {
  const { t } = useTranslation()

  const [sent, setSent] = useState(false)

  const resend = useMutation({
    mutationFn: resendConfirmation,
    onSuccess: () => setSent(true),
  })

  const isStaff = profile.roles.some((role) => role === 'Moderator' || role === 'Admin')

  return (
    <section className="card grid gap-3 p-5">
      <h2 className="eyebrow">{t('cabinet.access')}</h2>

      <div className="flex flex-wrap gap-1.5">
        {profile.roles.map((role) => (
          <span key={role} className={`pill ${role === 'User' ? '' : 'pill-accent'}`}>
            {roleLabel(t, role)}
          </span>
        ))}
        <span className="pill">
          {profile.accountType === 'Dealer'
            ? t('cabinet.dealerAccount')
            : t('cabinet.privateAccount')}
        </span>
      </div>

      {isStaff && (
        <Link to="/admin" className="btn justify-self-start">
          {t('cabinet.toAdmin')}
        </Link>
      )}

      <div className="border-t border-line pt-3">
        {profile.emailConfirmed ? (
          <p className="flex items-center gap-1.5 text-[13px] text-good">
            <VerifiedMark />
            {t('cabinet.emailConfirmed')}
          </p>
        ) : (
          <div className="grid gap-2">
            <p className="text-[13px] text-ink-2">
              {t('cabinet.emailUnconfirmed')}
            </p>

            {sent ? (
              <p className="text-[13px] text-good">{t('cabinet.letterSent')}</p>
            ) : (
              <button
                type="button"
                onClick={() => resend.mutate()}
                disabled={resend.isPending}
                className="btn justify-self-start"
              >
                {resend.isPending ? t('cabinet.sending') : t('cabinet.resend')}
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
  const { t } = useTranslation()

  const dealerships = useQuery({
    queryKey: ['my-dealerships'],
    queryFn: ({ signal }) => fetchMyDealerships(signal),
  })

  // Поки відповідь не приїхала, картку не малюємо: інакше на секунду
  // блимнуло б «створити салон» тому, у кого салон уже є.
  if (dealerships.isPending) {
    return null
  }

  const items = dealerships.data ?? []

  return (
    <section className="card grid gap-2 p-5">
      <h2 className="eyebrow">{t('cabinet.myDealerships')}</h2>

      {/*
        Картку показуємо навіть без жодного салону — саме звідси на нього
        заводяться. Раніше вона зникала цілком, і створити салон із
        інтерфейсу було нічим.
      */}
      <Link to="/my-dealership" className="justify-self-start text-[13px] text-accent hover:underline">
        {items.length > 0 ? t('cabinet.manageDealership') : t('cabinet.createDealership')}
      </Link>

      {items.map((membership) => (
        <div key={membership.dealershipId} className="flex items-center justify-between gap-3">
          <Link
            to={`/dealers/${membership.slug}`}
            className="flex items-center gap-1.5 text-[14px] font-semibold hover:text-accent"
          >
            {membership.isVerified && <VerifiedMark />}
            {membership.name}
          </Link>
          <span className="pill">
            {membership.role === 'Owner' ? t('cabinet.roleOwner') : t('cabinet.roleManager')}
          </span>
        </div>
      ))}
    </section>
  )
}

function roleLabel(t: (key: MessageKey) => string, role: string): string {
  if (role === 'Admin') return t('role.Admin')
  if (role === 'Moderator') return t('role.Moderator')

  return t('role.User')
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
