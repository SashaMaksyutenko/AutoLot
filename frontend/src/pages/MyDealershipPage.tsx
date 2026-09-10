import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError } from '../api/client'
import {
  addStaff,
  createDealership,
  fetchMyDealerships,
  fetchStaff,
  removeStaff,
  type DealershipMembership,
  type DealershipRole,
} from '../api/dealership'
import { fetchCities, fetchRegions } from '../api/reference'
import { useAuth } from '../auth/useAuth'
import { openSignIn } from '../auth/signInPrompt'
import { formatMonthYear } from '../format'
import { useTranslation } from '../i18n/useTranslation'

/**
 * Керування власним салоном: створення, персонал, ролі.
 *
 * Окремо від публічної вітрини (`/dealers/:slug`) навмисно. Вітрина — це те,
 * що бачить покупець, і вона має лишатися вітриною; тут же живуть дії, які
 * не показують нікому, крім працівників.
 */
export function MyDealershipPage() {
  const { t } = useTranslation()
  const auth = useAuth()

  const [selected, setSelected] = useState<number | null>(null)

  const memberships = useQuery({
    queryKey: ['my-dealerships'],
    queryFn: ({ signal }) => fetchMyDealerships(signal),
    enabled: auth.user !== null,
  })

  if (!auth.user) {
    return (
      <Notice>
        {t('salon.signInFirst')}{' '}
        <button type="button" onClick={openSignIn} className="text-accent hover:underline">
          {t('account.signIn')}
        </button>
      </Notice>
    )
  }

  if (memberships.isPending) return <Notice>{t('salon.loading')}</Notice>

  const items = memberships.data ?? []

  // Поки салону немає, сторінка складається з однієї форми: перелік з нуля
  // рядків і перемикач над ним були б порожньою рамкою.
  if (items.length === 0) {
    return (
      <div className="wrap grid gap-4 py-[26px]">
        <div>
          <h1 className="font-display text-[25px] font-bold">{t('salon.createTitle')}</h1>
          <p className="text-[13px] text-ink-2">{t('salon.createLead')}</p>
        </div>

        <CreateForm onCreated={() => void memberships.refetch()} />
      </div>
    )
  }

  const current = items.find((item) => item.dealershipId === selected) ?? items[0]

  return (
    <div className="wrap grid gap-4 py-[26px]">
      <div>
        <h1 className="font-display text-[25px] font-bold">{t('salon.title')}</h1>
        <p className="text-[13px] text-ink-2">{t('salon.lead')}</p>
      </div>

      {/* Перемикач має сенс лише тому, хто працює більш ніж в одному салоні. */}
      {items.length > 1 && (
        <div className="flex flex-wrap gap-1.5">
          {items.map((item) => (
            <button
              key={item.dealershipId}
              type="button"
              onClick={() => setSelected(item.dealershipId)}
              aria-pressed={item.dealershipId === current.dealershipId}
              className={`rounded-control border px-3 py-1.5 text-[13px] ${
                item.dealershipId === current.dealershipId
                  ? 'border-accent bg-accent-soft font-semibold text-accent'
                  : 'border-line text-ink-2 hover:border-ink-3'
              }`}
            >
              {item.name}
            </button>
          ))}
        </div>
      )}

      <Header membership={current} />
      <StaffPanel membership={current} />
    </div>
  )
}

function Header({ membership }: { membership: DealershipMembership }) {
  const { t } = useTranslation()

  return (
    <section className="card grid gap-2 p-4">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-display text-[19px] font-bold">{membership.name}</span>

        <span className={`pill ${membership.isVerified ? 'pill-good' : ''}`}>
          {membership.isVerified ? t('salon.verified') : t('salon.awaitingVerification')}
        </span>

        <span className="pill">
          {membership.role === 'Owner' ? t('cabinet.roleOwner') : t('cabinet.roleManager')}
        </span>
      </div>

      <Link
        to={`/dealers/${membership.slug}`}
        className="justify-self-start text-[13px] text-accent hover:underline"
      >
        {t('salon.openShowcase')}
      </Link>

      {!membership.isVerified && (
        <p className="text-[12.5px] text-ink-3">{t('salon.verificationNote')}</p>
      )}
    </section>
  )
}

/**
 * Персонал салону.
 *
 * Менеджер бачить перелік, але не бачить кнопок: право змінювати склад є
 * лише у власника. Ховаємо саме кнопки, а не весь блок — знати, з ким
 * працюєш, потрібно обом.
 */
function StaffPanel({ membership }: { membership: DealershipMembership }) {
  const { t } = useTranslation()
  const auth = useAuth()
  const queryClient = useQueryClient()

  const isOwner = membership.role === 'Owner'
  const [error, setError] = useState<string | null>(null)

  const staff = useQuery({
    queryKey: ['dealership-staff', membership.dealershipId],
    queryFn: ({ signal }) => fetchStaff(membership.dealershipId, signal),
  })

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ['dealership-staff', membership.dealershipId] })

  const remove = useMutation({
    mutationFn: (userId: number) => removeStaff(membership.dealershipId, userId),
    onSuccess: () => {
      setError(null)
      void refresh()
    },
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : t('error.network')),
  })

  return (
    <section className="card grid gap-3 p-4">
      <h2 className="eyebrow">{t('salon.staff')}</h2>

      {staff.isPending && <p className="text-[13px] text-ink-2">{t('salon.loading')}</p>}

      {staff.data && (
        <ul className="grid">
          {staff.data.map((person) => (
            <li
              key={person.userId}
              className="flex flex-wrap items-center gap-x-3 gap-y-1 border-b border-line py-2 last:border-0"
            >
              <span className="text-[14px] font-semibold">{person.displayName}</span>
              <span className="text-[12.5px] text-ink-3">{person.email}</span>

              <span className="pill">
                {person.role === 'Owner' ? t('cabinet.roleOwner') : t('cabinet.roleManager')}
              </span>

              <span className="text-[12px] text-ink-3">
                {t('salon.since', { date: formatMonthYear(person.joinedAt) })}
              </span>

              {/*
                Себе зі списку не прибирають: випадково натиснути «прибрати»
                навпроти власного імені легко, а повернутися вже нічим —
                доступ зникає тієї ж миті. Хто хоче піти, того прибирає
                інший власник. Правило «останнього власника не чіпати»
                тримає сервер, і його текст помилки тут і показуємо.
              */}
              {isOwner && person.userId !== auth.user?.id && (
                <button
                  type="button"
                  onClick={() => remove.mutate(person.userId)}
                  disabled={remove.isPending}
                  className="ml-auto text-[12.5px] text-ink-3 hover:text-danger"
                >
                  {t('salon.remove')}
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      {error && (
        <p className="rounded-control bg-danger-soft px-3 py-2 text-[13px] text-danger">{error}</p>
      )}

      {isOwner ? (
        <AddStaffForm dealershipId={membership.dealershipId} onAdded={refresh} />
      ) : (
        <p className="text-[12.5px] text-ink-3">{t('salon.managerNote')}</p>
      )}
    </section>
  )
}

function AddStaffForm({
  dealershipId,
  onAdded,
}: {
  dealershipId: number
  onAdded: () => void
}) {
  const { t } = useTranslation()

  const [email, setEmail] = useState('')
  const [role, setRole] = useState<DealershipRole>('Manager')
  const [error, setError] = useState<string | null>(null)

  const add = useMutation({
    mutationFn: () => addStaff(dealershipId, email.trim(), role),
    onSuccess: () => {
      setEmail('')
      setError(null)
      onAdded()
    },
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : t('error.network')),
  })

  return (
    <form
      className="grid gap-2 border-t border-line pt-3"
      onSubmit={(event) => {
        event.preventDefault()
        add.mutate()
      }}
    >
      <span className="text-[11.5px] font-semibold text-ink-2">{t('salon.addStaff')}</span>

      <div className="flex flex-wrap gap-2">
        <input
          type="email"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          placeholder={t('salon.emailPlaceholder')}
          required
          className="control min-w-[220px] flex-1"
        />

        <select
          value={role}
          onChange={(event) => setRole(event.target.value as DealershipRole)}
          className="control w-auto shrink-0"
        >
          <option value="Manager">{t('cabinet.roleManager')}</option>
          <option value="Owner">{t('cabinet.roleOwner')}</option>
        </select>

        <button
          type="submit"
          disabled={add.isPending || email.trim().length === 0}
          className="btn btn-primary shrink-0"
        >
          {add.isPending ? t('salon.adding') : t('salon.add')}
        </button>
      </div>

      <span className="text-[12px] text-ink-3">{t('salon.addHint')}</span>

      {error && (
        <p className="rounded-control bg-danger-soft px-3 py-2 text-[13px] text-danger">{error}</p>
      )}
    </form>
  )
}

function CreateForm({ onCreated }: { onCreated: () => void }) {
  const { t } = useTranslation()

  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [regionId, setRegionId] = useState<number | undefined>()
  const [cityId, setCityId] = useState<number>(0)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

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

  const create = useMutation({
    mutationFn: () =>
      createDealership({ name: name.trim(), description: description.trim() || null, cityId }),
    onSuccess: onCreated,
    onError: (caught) => {
      if (caught instanceof ApiError) {
        setError(caught.message)
        setFieldErrors(caught.errors)
      } else {
        setError(t('error.network'))
      }
    },
  })

  return (
    <form
      className="card grid gap-3 p-4"
      onSubmit={(event) => {
        event.preventDefault()
        setError(null)
        setFieldErrors({})
        create.mutate()
      }}
    >
      <Field label={t('salon.name')} errors={fieldErrors.Name} hint={t('salon.nameHint')}>
        <input
          value={name}
          onChange={(event) => setName(event.target.value)}
          maxLength={120}
          required
          className="control"
        />
      </Field>

      <Field label={t('salon.description')} errors={fieldErrors.Description}>
        <textarea
          value={description}
          onChange={(event) => setDescription(event.target.value)}
          rows={4}
          maxLength={2000}
          className="control resize-y"
        />
      </Field>

      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t('filter.region')}>
          <select
            value={regionId ?? ''}
            // Місто належить області — зміна області скидає обране місто.
            onChange={(event) => {
              setRegionId(event.target.value ? Number(event.target.value) : undefined)
              setCityId(0)
            }}
            className="control"
          >
            <option value="">{t('filter.wholeCountry')}</option>
            {regions.data?.map((region) => (
              <option key={region.id} value={region.id}>
                {region.name}
              </option>
            ))}
          </select>
        </Field>

        <Field label={t('form.city')} errors={fieldErrors.CityId}>
          <select
            value={cityId || ''}
            disabled={regionId === undefined}
            onChange={(event) => setCityId(Number(event.target.value))}
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
      </div>

      {error && (
        <p className="rounded-control bg-danger-soft px-3 py-2 text-[13px] text-danger">{error}</p>
      )}

      <button
        type="submit"
        disabled={create.isPending || name.trim().length === 0 || cityId === 0}
        className="btn btn-primary justify-self-start px-6 py-2.5"
      >
        {create.isPending ? t('salon.creating') : t('salon.create')}
      </button>

      <p className="text-[12.5px] text-ink-3">{t('salon.createNote')}</p>
    </form>
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
    <label className="flex min-w-0 flex-col gap-1.5">
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
  return <div className="wrap py-16 text-center text-sm text-ink-2">{children}</div>
}
