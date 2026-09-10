import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { ApiError } from '../api/client'
import type { Currency } from '../api/catalog'
import {
  createListing,
  fetchListingDraft,
  updateListing,
  type CarSpecificationInput,
  type ListingInput,
} from '../api/listingForm'
import { fetchMyDealerships } from '../api/dealership'
import {
  fetchCarAttributes,
  fetchCities,
  fetchCityDistricts,
  fetchCountries,
  fetchGenerations,
  fetchMakes,
  fetchModels,
  fetchRegions,
  type LookupItem,
} from '../api/reference'
import { useAuth } from '../auth/useAuth'
import { openSignIn } from '../auth/signInPrompt'
import { FeaturePicker } from '../components/catalog/FeaturePicker'
import { PhotoManager } from '../components/listing/PhotoManager'
import { useTranslation } from '../i18n/useTranslation'
import type { MessageKey } from '../i18n/messages'

/**
 * Форма оголошення — одна на подання й на редагування.
 *
 * Один компонент на два випадки навмисно: поля однакові до єдиного, а
 * різниця зводиться до трьох речей — звідки взявся початковий стан, куди
 * піде збереження і чи можна ще змінити тип продажу. Дві копії такої форми
 * розійшлися б при першій же новій характеристиці авто.
 *
 * Тип продажу після подання не міняється: перетворити оголошення з ціною на
 * аукціонний лот означало б переписати правила гри посеред неї. Тому при
 * редагуванні перемикач заблокований, а не прихований — так видно, що вибір
 * був, і який саме.
 */
export function ListingFormPage() {
  const { t } = useTranslation()
  const auth = useAuth()
  const navigate = useNavigate()

  // Форма живе за двома адресами: /sell і /listing/:id/edit.
  const { id } = useParams()
  const listingId = id ? Number(id) : null
  const isEditing = listingId !== null

  const [form, setForm] = useState<ListingInput>(emptyListing)
  const [regionId, setRegionId] = useState<number | undefined>()
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  const draft = useQuery({
    queryKey: ['listing-draft', listingId],
    queryFn: ({ signal }) => fetchListingDraft(listingId!, signal),
    enabled: isEditing,

    // Чернетку читаємо один раз: далі джерело істини — те, що людина набрала.
    // Без цього фонове оновлення кешу затерло б незбережені правки.
    staleTime: Infinity,
    refetchOnMount: false,
  })

  // Заповнюємо форму, щойно чернетка приїхала. Саме в ефекті, а не під час
  // малювання: setState посеред рендера — це нескінченний цикл.
  useEffect(() => {
    if (!draft.data) return

    const { id: _id, regionId: draftRegion, status: _status, rejectionReason: _reason, ...rest } =
      draft.data

    setForm(rest)
    setRegionId(draftRegion)
  }, [draft.data])

  const save = useMutation({
    mutationFn: () =>
      isEditing
        ? updateListing(listingId, form).then(() => listingId)
        : createListing(form).then((created) => created.id),

    // Створене оголошення відкриваємо в редагуванні, а не в кабінеті:
    // наступний крок — фото, а вони потребують ідентифікатора, якого до
    // збереження ще не існувало.
    onSuccess: (savedId) =>
      navigate(`/listing/${savedId}/edit`, { replace: !isEditing }),

    onError: (caught) => {
      if (caught instanceof ApiError) {
        setError(caught.message)
        setFieldErrors(caught.errors)
      } else {
        setError(t('error.network'))
      }
    },
  })

  if (!auth.user) {
    return (
      <Notice>
        {t('form.signInFirst')}{' '}
        <button type="button" onClick={openSignIn} className="text-accent hover:underline">
          {t('account.signIn')}
        </button>
      </Notice>
    )
  }

  if (isEditing && draft.isPending) {
    return <Notice>{t('form.loading')}</Notice>
  }

  if (isEditing && draft.isError) {
    return (
      <Notice>
        {t('form.notFound')}{' '}
        <Link to="/account" className="text-accent hover:underline">
          {t('form.toAccount')}
        </Link>
      </Notice>
    )
  }

  /** Точкова зміна поля оголошення. */
  function patch(change: Partial<ListingInput>) {
    setForm((current) => ({ ...current, ...change }))
  }

  /** Те саме для характеристик авто — вони лежать на рівень глибше. */
  function patchCar(change: Partial<CarSpecificationInput>) {
    setForm((current) => ({ ...current, car: { ...current.car, ...change } }))
  }

  const missing = missingFields(form)

  return (
    <div className="wrap grid gap-4 py-[26px]">
      <div>
        <h1 className="font-display text-[25px] font-bold">
          {isEditing ? t('form.editTitle') : t('form.createTitle')}
        </h1>
        <p className="text-[13px] text-ink-2">{t('form.lead')}</p>
      </div>

      {/* Відхилене оголошення відкривають саме заради зауваження — показуємо його першим. */}
      {draft.data?.rejectionReason && (
        <p className="card border-danger p-4 text-[13.5px]">
          <span className="eyebrow mb-1 block">{t('form.rejectionReason')}</span>
          {draft.data.rejectionReason}
        </p>
      )}

      <form
        className="grid gap-4"
        onSubmit={(event) => {
          event.preventDefault()
          setError(null)
          setFieldErrors({})
          save.mutate()
        }}
      >
        <SaleSection
          form={form}
          patch={patch}
          isEditing={isEditing}
          fieldErrors={fieldErrors}
        />

        <CarSection form={form} patchCar={patchCar} fieldErrors={fieldErrors} />
        <EngineSection form={form} patchCar={patchCar} />
        <BodySection form={form} patchCar={patchCar} />
        <HistorySection form={form} patchCar={patchCar} />

        <Section title={t('filter.features')}>
          <FeaturePicker
            selected={form.car.featureIds}
            onChange={(featureIds) => patchCar({ featureIds })}
          />
        </Section>

        <PlaceSection
          form={form}
          patch={patch}
          regionId={regionId}
          onRegion={setRegionId}
          fieldErrors={fieldErrors}
        />

        <PriceSection form={form} patch={patch} fieldErrors={fieldErrors} />

        {/*
          Фото прив’язані до оголошення, тож до першого збереження їх нема
          до чого чіпляти. Замість порожнього блока пояснюємо це рядком.
        */}
        {isEditing ? (
          <PhotoManager listingId={listingId} />
        ) : (
          <p className="text-[12.5px] text-ink-3">{t('form.photosAfterSave')}</p>
        )}

        {error && (
          <p className="rounded-control bg-danger-soft px-3 py-2 text-[13px] text-danger">
            {error}
          </p>
        )}

        {/*
          Перелічуємо, чого бракує, ще до надсилання. Сервер перевірить те
          саме й точніше, але змушувати людину дізнаватися про порожню марку
          з відповіді сервера — зайвий круг.
        */}
        {missing.length > 0 && (
          <p className="text-[12.5px] text-ink-3">
            {t('form.stillNeeded')} {missing.map((key) => t(key)).join(', ')}
          </p>
        )}

        <div className="flex flex-wrap gap-2">
          <button
            type="submit"
            disabled={save.isPending || missing.length > 0}
            className="btn btn-primary px-6 py-2.5"
          >
            {save.isPending ? t('form.saving') : isEditing ? t('form.save') : t('form.create')}
          </button>

          <Link to="/account" className="btn py-2.5">
            {t('form.cancel')}
          </Link>
        </div>

        <p className="text-[12.5px] text-ink-3">{t('form.draftNote')}</p>
      </form>
    </div>
  )
}

// ── Розділи форми ──────────────────────────────────────────────────────
//
// Кожен розділ — окремий компонент не заради повторного використання, а
// заради читаності: одна функція на сімдесят полів не вміщується в голові.

function SaleSection({
  form,
  patch,
  isEditing,
  fieldErrors,
}: {
  form: ListingInput
  patch: (change: Partial<ListingInput>) => void
  isEditing: boolean
  fieldErrors: Record<string, string[]>
}) {
  const { t } = useTranslation()

  const dealerships = useQuery({
    queryKey: ['my-dealerships'],
    queryFn: ({ signal }) => fetchMyDealerships(signal),
  })

  return (
    <Section title={t('form.saleSection')}>
      <Field label={t('filter.saleType')} hint={isEditing ? t('form.typeLocked') : undefined}>
        <div className="flex overflow-hidden rounded-control border border-line">
          <Choice
            label={t('filter.fixedPrice')}
            active={form.type === 'FixedPrice'}
            disabled={isEditing}
            onClick={() => patch({ type: 'FixedPrice', reservePrice: null })}
          />
          <Choice
            label={t('filter.auction')}
            active={form.type === 'Auction'}
            disabled={isEditing}
            onClick={() => patch({ type: 'Auction' })}
          />
        </div>
      </Field>

      {/* Салон пропонуємо лише тим, хто в ньому працює. */}
      {!isEditing && (dealerships.data?.length ?? 0) > 0 && (
        <Field label={t('form.onBehalfOf')}>
          <select
            value={form.dealershipId ?? ''}
            onChange={(event) =>
              patch({ dealershipId: event.target.value ? Number(event.target.value) : null })
            }
            className="control"
          >
            <option value="">{t('form.asPrivate')}</option>
            {dealerships.data?.map((membership) => (
              <option key={membership.dealershipId} value={membership.dealershipId}>
                {membership.name}
              </option>
            ))}
          </select>
        </Field>
      )}

      <Field label={t('form.title')} errors={fieldErrors.Title} hint={t('form.titleHint')}>
        <input
          value={form.title}
          onChange={(event) => patch({ title: event.target.value })}
          maxLength={120}
          className="control"
        />
      </Field>

      <Field
        label={t('form.description')}
        errors={fieldErrors.Description}
        hint={t('form.descriptionHint')}
      >
        <textarea
          value={form.description}
          onChange={(event) => patch({ description: event.target.value })}
          rows={6}
          maxLength={5000}
          className="control resize-y"
        />
      </Field>
    </Section>
  )
}

function CarSection({
  form,
  patchCar,
  fieldErrors,
}: {
  form: ListingInput
  patchCar: (change: Partial<CarSpecificationInput>) => void
  fieldErrors: Record<string, string[]>
}) {
  const { t } = useTranslation()
  const car = form.car

  const attributes = useQuery({
    queryKey: ['car-attributes'],
    queryFn: ({ signal }) => fetchCarAttributes(signal),
    staleTime: Infinity,
  })

  const makes = useQuery({
    queryKey: ['makes'],
    queryFn: ({ signal }) => fetchMakes(signal),
    staleTime: Infinity,
  })

  const models = useQuery({
    queryKey: ['models', car.makeId],
    queryFn: ({ signal }) => fetchModels(car.makeId, signal),
    enabled: car.makeId > 0,
    staleTime: Infinity,
  })

  const generations = useQuery({
    queryKey: ['generations', car.modelId],
    queryFn: ({ signal }) => fetchGenerations(car.modelId, signal),
    enabled: car.modelId > 0,
    staleTime: Infinity,
  })

  return (
    <Section title={t('form.carSection')}>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t('filter.makeModel')} errors={fieldErrors['Car.MakeId']}>
          <select
            value={car.makeId || ''}
            // Модель належить марці, тож зміна марки скидає модель і покоління.
            onChange={(event) =>
              patchCar({ makeId: Number(event.target.value), modelId: 0, generationId: null })
            }
            className="control"
          >
            <option value="">{t('filter.anyMake')}</option>
            {makes.data?.map((make) => (
              <option key={make.id} value={make.id}>
                {make.name}
              </option>
            ))}
          </select>
        </Field>

        <Field label={t('form.model')} errors={fieldErrors['Car.ModelId']}>
          <select
            value={car.modelId || ''}
            disabled={car.makeId === 0}
            onChange={(event) =>
              patchCar({ modelId: Number(event.target.value), generationId: null })
            }
            className="control"
          >
            <option value="">
              {car.makeId ? t('filter.anyModel') : t('filter.chooseMakeFirst')}
            </option>
            {models.data?.map((model) => (
              <option key={model.id} value={model.id}>
                {model.name}
              </option>
            ))}
          </select>
        </Field>

        {(generations.data?.length ?? 0) > 0 && (
          <Field label={t('form.generation')}>
            <select
              value={car.generationId ?? ''}
              onChange={(event) =>
                patchCar({ generationId: event.target.value ? Number(event.target.value) : null })
              }
              className="control"
            >
              <option value="">{t('filter.anyGeneration')}</option>
              {generations.data?.map((generation) => (
                <option key={generation.id} value={generation.id}>
                  {generation.name}
                </option>
              ))}
            </select>
          </Field>
        )}

        <Field label={t('spec.year')} errors={fieldErrors['Car.Year']}>
          <Num value={car.year} onChange={(year) => patchCar({ year: year ?? 0 })} />
        </Field>

        <Field label={t('spec.condition')}>
          <Lookup
            options={attributes.data?.conditions}
            value={car.condition}
            onChange={(condition) => patchCar({ condition })}
          />
        </Field>

        <Field label={t('spec.mileage')} errors={fieldErrors['Car.Mileage']}>
          <Num value={car.mileage} onChange={(mileage) => patchCar({ mileage })} />
        </Field>

        <Field label={t('form.vin')} hint={t('form.vinHint')} errors={fieldErrors['Car.Vin']}>
          <input
            value={car.vin ?? ''}
            onChange={(event) => patchCar({ vin: event.target.value || null })}
            maxLength={17}
            className="control font-mono uppercase"
          />
        </Field>
      </div>
    </Section>
  )
}

function EngineSection({
  form,
  patchCar,
}: {
  form: ListingInput
  patchCar: (change: Partial<CarSpecificationInput>) => void
}) {
  const { t } = useTranslation()
  const car = form.car

  const attributes = useQuery({
    queryKey: ['car-attributes'],
    queryFn: ({ signal }) => fetchCarAttributes(signal),
    staleTime: Infinity,
  })

  // Поля батареї показуємо лише електричним і гібридним: запас ходу під
  // бензиновим авто — просто шум, як і в панелі фільтрів.
  const isElectric = car.fuelType === 'Electric' || car.fuelType === 'PluginHybrid'

  return (
    <Section title={t('form.engineSection')}>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t('filter.fuel')}>
          <Lookup
            options={attributes.data?.fuelTypes}
            value={car.fuelType}
            onChange={(fuelType) => patchCar({ fuelType })}
          />
        </Field>

        <Field label={t('filter.transmission')}>
          <Lookup
            options={attributes.data?.transmissions}
            value={car.transmission}
            onChange={(transmission) => patchCar({ transmission })}
          />
        </Field>

        <Field label={t('filter.drivetrain')}>
          <Lookup
            options={attributes.data?.driveTypes}
            value={car.drivetrain}
            onChange={(drivetrain) => patchCar({ drivetrain })}
          />
        </Field>

        <Field label={t('filter.engineVolume')}>
          <Num step="0.1" value={car.engineVolume} onChange={(v) => patchCar({ engineVolume: v })} />
        </Field>

        <Field label={t('filter.power')}>
          <Num value={car.enginePower} onChange={(v) => patchCar({ enginePower: v })} />
        </Field>

        <Field label={t('spec.consumption')}>
          <Num
            step="0.1"
            value={car.fuelConsumptionCombined}
            onChange={(v) => patchCar({ fuelConsumptionCombined: v })}
          />
        </Field>

        {isElectric && (
          <>
            <Field label={t('filter.battery')}>
              <Num
                step="0.1"
                value={car.batteryCapacity}
                onChange={(v) => patchCar({ batteryCapacity: v })}
              />
            </Field>

            <Field label={t('filter.range')}>
              <Num value={car.electricRange} onChange={(v) => patchCar({ electricRange: v })} />
            </Field>

            <Field label={t('filter.chargingPort')}>
              <Lookup
                options={attributes.data?.chargingPorts}
                value={car.chargingPort ?? ''}
                allowEmpty
                onChange={(chargingPort) => patchCar({ chargingPort: chargingPort || null })}
              />
            </Field>
          </>
        )}
      </div>
    </Section>
  )
}

function BodySection({
  form,
  patchCar,
}: {
  form: ListingInput
  patchCar: (change: Partial<CarSpecificationInput>) => void
}) {
  const { t } = useTranslation()
  const car = form.car

  const attributes = useQuery({
    queryKey: ['car-attributes'],
    queryFn: ({ signal }) => fetchCarAttributes(signal),
    staleTime: Infinity,
  })

  return (
    <Section title={t('form.bodySection')}>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t('filter.body')}>
          <Lookup
            options={attributes.data?.bodyTypes}
            value={car.bodyType}
            onChange={(bodyType) => patchCar({ bodyType })}
          />
        </Field>

        <Field label={t('filter.colour')}>
          <Lookup
            options={attributes.data?.colors}
            value={car.color}
            onChange={(color) => patchCar({ color })}
          />
        </Field>

        <Field label={t('spec.seats')}>
          <Num value={car.seatCount} onChange={(seatCount) => patchCar({ seatCount })} />
        </Field>

        <Field label={t('spec.doors')}>
          <Num value={car.doorCount} onChange={(doorCount) => patchCar({ doorCount })} />
        </Field>
      </div>

      <Toggle
        label={t('filter.metallic')}
        checked={car.isMetallic}
        onChange={(isMetallic) => patchCar({ isMetallic })}
      />
    </Section>
  )
}

function HistorySection({
  form,
  patchCar,
}: {
  form: ListingInput
  patchCar: (change: Partial<CarSpecificationInput>) => void
}) {
  const { t } = useTranslation()
  const car = form.car

  const attributes = useQuery({
    queryKey: ['car-attributes'],
    queryFn: ({ signal }) => fetchCarAttributes(signal),
    staleTime: Infinity,
  })

  const countries = useQuery({
    queryKey: ['countries'],
    queryFn: ({ signal }) => fetchCountries(signal),
    staleTime: Infinity,
  })

  return (
    <Section title={t('filter.origin')}>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t('spec.owners')}>
          <Num value={car.ownerCount} onChange={(ownerCount) => patchCar({ ownerCount })} />
        </Field>

        <Field label={t('filter.ecology')}>
          <Lookup
            options={attributes.data?.ecologyStandards}
            value={car.ecologyStandard ?? ''}
            allowEmpty
            onChange={(value) => patchCar({ ecologyStandard: value || null })}
          />
        </Field>

        <Field label={t('filter.damage')}>
          <Lookup
            options={attributes.data?.damageStates}
            value={car.damageState}
            onChange={(damageState) => patchCar({ damageState })}
          />
        </Field>

        <Field label={t('filter.paint')}>
          <Lookup
            options={attributes.data?.paintConditions}
            value={car.paintCondition ?? ''}
            allowEmpty
            onChange={(value) => patchCar({ paintCondition: value || null })}
          />
        </Field>

        <Field label={t('filter.manufacturerCountry')}>
          <Country
            value={car.manufacturerCountryId}
            options={countries.data}
            onChange={(manufacturerCountryId) => patchCar({ manufacturerCountryId })}
          />
        </Field>

        <Field label={t('filter.importedFrom')}>
          <Country
            value={car.importedFromCountryId}
            options={countries.data}
            onChange={(importedFromCountryId) => patchCar({ importedFromCountryId })}
          />
        </Field>
      </div>

      <div className="grid gap-1.5">
        <Toggle
          label={t('fact.accident')}
          checked={car.wasInAccident}
          onChange={(wasInAccident) => patchCar({ wasInAccident })}
        />
        <Toggle
          label={t('fact.customsCleared')}
          checked={car.isCustomsCleared}
          onChange={(isCustomsCleared) => patchCar({ isCustomsCleared })}
        />
        <Toggle
          label={t('fact.inUkraine')}
          checked={car.isLocatedInUkraine}
          onChange={(isLocatedInUkraine) => patchCar({ isLocatedInUkraine })}
        />
        <Toggle
          label={t('fact.serviceBook')}
          checked={car.hasServiceBook}
          onChange={(hasServiceBook) => patchCar({ hasServiceBook })}
        />
        <Toggle
          label={t('fact.garageKept')}
          checked={car.isGarageKept}
          onChange={(isGarageKept) => patchCar({ isGarageKept })}
        />
        <Toggle
          label={t('fact.onCredit')}
          checked={car.isOnCredit}
          onChange={(isOnCredit) => patchCar({ isOnCredit })}
        />
      </div>
    </Section>
  )
}

function PlaceSection({
  form,
  patch,
  regionId,
  onRegion,
  fieldErrors,
}: {
  form: ListingInput
  patch: (change: Partial<ListingInput>) => void
  regionId: number | undefined
  onRegion: (value: number | undefined) => void
  fieldErrors: Record<string, string[]>
}) {
  const { t } = useTranslation()

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
    queryKey: ['city-districts', form.cityId],
    queryFn: ({ signal }) => fetchCityDistricts(form.cityId, signal),
    enabled: form.cityId > 0,
    staleTime: Infinity,
  })

  return (
    <Section title={t('filter.region')}>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t('filter.region')}>
          <select
            value={regionId ?? ''}
            // Місто належить області — зміна області скидає місто й район.
            onChange={(event) => {
              onRegion(event.target.value ? Number(event.target.value) : undefined)
              patch({ cityId: 0, cityDistrictId: null })
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
            value={form.cityId || ''}
            disabled={regionId === undefined}
            onChange={(event) =>
              patch({ cityId: Number(event.target.value), cityDistrictId: null })
            }
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

        {(districts.data?.length ?? 0) > 0 && (
          <Field label={t('form.district')}>
            <select
              value={form.cityDistrictId ?? ''}
              onChange={(event) =>
                patch({ cityDistrictId: event.target.value ? Number(event.target.value) : null })
              }
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
    </Section>
  )
}

function PriceSection({
  form,
  patch,
  fieldErrors,
}: {
  form: ListingInput
  patch: (change: Partial<ListingInput>) => void
  fieldErrors: Record<string, string[]>
}) {
  const { t } = useTranslation()

  return (
    <Section title={t('filter.price')}>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field
          label={form.type === 'Auction' ? t('form.startingPrice') : t('filter.price')}
          errors={fieldErrors.Price}
        >
          <div className="flex gap-2">
            <Num value={form.price} onChange={(price) => patch({ price: price ?? 0 })} />

            <select
              value={form.currency}
              onChange={(event) => patch({ currency: event.target.value as Currency })}
              className="control w-auto shrink-0"
            >
              <option value="Usd">$</option>
              <option value="Eur">€</option>
              <option value="Uah">₴</option>
            </select>
          </div>
        </Field>

        {/* Резервна ціна буває лише в лота з торгами — так каже і сервер. */}
        {form.type === 'Auction' && (
          <Field
            label={t('form.reservePrice')}
            hint={t('form.reserveHint')}
            errors={fieldErrors.ReservePrice}
          >
            <Num
              value={form.reservePrice}
              onChange={(reservePrice) => patch({ reservePrice })}
            />
          </Field>
        )}
      </div>

      <div className="grid gap-1.5">
        <Toggle
          label={t('filter.negotiable')}
          checked={form.isNegotiable}
          onChange={(isNegotiable) => patch({ isNegotiable })}
        />
        <Toggle
          label={t('listing.acceptsTrade')}
          checked={form.acceptsTrade}
          onChange={(acceptsTrade) => patch({ acceptsTrade })}
        />
        <Toggle
          label={t('filter.urgent')}
          checked={form.isUrgent}
          onChange={(isUrgent) => patch({ isUrgent })}
        />
      </div>
    </Section>
  )
}

// ── Дрібні будівельні блоки ────────────────────────────────────────────

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="card grid gap-3 p-4">
      <h2 className="eyebrow">{title}</h2>
      {children}
    </section>
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

/**
 * Список значень довідника. Поки довідник не приїхав, показуємо порожній
 * список, а не заглушку: перемальовування зі «завантажуємо» на список
 * смикало б усю форму.
 */
function Lookup({
  options,
  value,
  allowEmpty,
  onChange,
}: {
  options: LookupItem[] | undefined
  value: string
  allowEmpty?: boolean
  onChange: (value: string) => void
}) {
  const { t } = useTranslation()

  return (
    <select
      value={value}
      onChange={(event) => onChange(event.target.value)}
      className="control"
    >
      {(allowEmpty || value === '') && <option value="">{t('form.notChosen')}</option>}
      {options?.map((option) => (
        <option key={option.value} value={option.value}>
          {option.name}
        </option>
      ))}
    </select>
  )
}

function Country({
  value,
  options,
  onChange,
}: {
  value: number | null | undefined
  options: { id: number; name: string }[] | undefined
  onChange: (value: number | null) => void
}) {
  const { t } = useTranslation()

  return (
    <select
      value={value ?? ''}
      onChange={(event) => onChange(event.target.value ? Number(event.target.value) : null)}
      className="control"
    >
      <option value="">{t('form.notChosen')}</option>
      {options?.map((country) => (
        <option key={country.id} value={country.id}>
          {country.name}
        </option>
      ))}
    </select>
  )
}

/**
 * Числове поле. Порожнє означає «не вказано» і перетворюється на null, а не
 * на нуль: нуль дверей і «скільки дверей — не знаю» — різні речі.
 */
function Num({
  value,
  step,
  onChange,
}: {
  value: number | null | undefined
  step?: string
  onChange: (value: number | null) => void
}) {
  return (
    <input
      type="number"
      step={step}
      inputMode={step ? 'decimal' : 'numeric'}
      value={value ?? ''}
      onChange={(event) => onChange(event.target.value ? Number(event.target.value) : null)}
      className="control min-w-0 font-mono tabular-nums"
    />
  )
}

function Toggle({
  label,
  checked,
  onChange,
}: {
  label: string
  checked: boolean
  onChange: (checked: boolean) => void
}) {
  return (
    <label className="flex cursor-pointer items-center gap-2 text-[13.5px]">
      <input
        type="checkbox"
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
        className="h-[15px] w-[15px] accent-accent"
      />
      <span>{label}</span>
    </label>
  )
}

function Choice({
  label,
  active,
  disabled,
  onClick,
}: {
  label: string
  active: boolean
  disabled?: boolean
  onClick: () => void
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      aria-pressed={active}
      className={`flex-1 border-line px-1 py-1.5 text-[13px] not-first:border-l ${
        active ? 'bg-accent-soft font-semibold text-accent' : 'bg-surface text-ink-2'
      } ${disabled ? 'cursor-not-allowed opacity-60' : ''}`}
    >
      {label}
    </button>
  )
}

function Notice({ children }: { children: React.ReactNode }) {
  return <div className="wrap py-16 text-center text-sm text-ink-2">{children}</div>
}

// ── Дані ───────────────────────────────────────────────────────────────

/**
 * Порожнє оголошення. Перелічення лишаємо порожніми рядками, а не вгадуємо
 * перше значення довідника: «Седан» за замовчуванням тихо потрапив би в
 * половину оголошень, які ніхто не дочитав до кінця.
 */
const emptyListing: ListingInput = {
  title: '',
  description: '',
  cityId: 0,
  cityDistrictId: null,
  price: 0,
  currency: 'Usd',
  reservePrice: null,
  type: 'FixedPrice',
  isNegotiable: false,
  acceptsTrade: false,
  isUrgent: false,
  dealershipId: null,
  car: {
    vin: null,
    year: new Date().getFullYear(),
    condition: 'Used',
    makeId: 0,
    modelId: 0,
    generationId: null,
    mileage: null,
    ownerCount: null,
    fuelType: '',
    engineVolume: null,
    enginePower: null,
    fuelConsumptionCity: null,
    fuelConsumptionHighway: null,
    fuelConsumptionCombined: null,
    batteryCapacity: null,
    electricRange: null,
    chargingPort: null,
    transmission: '',
    drivetrain: '',
    bodyType: '',
    color: '',
    isMetallic: false,
    seatCount: null,
    doorCount: null,
    ecologyStandard: null,
    manufacturerCountryId: null,
    importedFromCountryId: null,
    isCustomsCleared: true,
    isLocatedInUkraine: true,
    wasInAccident: false,
    damageState: 'NotDamaged',
    paintCondition: null,
    hasServiceBook: false,
    isGarageKept: false,
    isOnCredit: false,
    featureIds: [],
  },
}

/**
 * Чого ще бракує для надсилання.
 *
 * Перевіряємо не все, що перевіряє сервер, а лише порожні обов'язкові поля.
 * Довжину заголовка й межі року лишаємо серверу: дублювати його правила тут
 * означало б завести другий набір, який рано чи пізно розійдеться з першим.
 * Порожні ж перелічення ловимо самі — сервер відповів би на них технічною
 * помилкою розбору JSON, а не зрозумілим текстом.
 */
function missingFields(form: ListingInput): MessageKey[] {
  const missing: MessageKey[] = []

  if (form.title.trim().length === 0) missing.push('form.title')
  if (form.description.trim().length === 0) missing.push('form.description')
  if (form.car.makeId === 0) missing.push('filter.makeModel')
  if (form.car.modelId === 0) missing.push('form.model')
  if (form.car.fuelType === '') missing.push('filter.fuel')
  if (form.car.transmission === '') missing.push('filter.transmission')
  if (form.car.drivetrain === '') missing.push('filter.drivetrain')
  if (form.car.bodyType === '') missing.push('filter.body')
  if (form.car.color === '') missing.push('filter.colour')
  if (form.cityId === 0) missing.push('form.city')
  if (form.price <= 0) missing.push('filter.price')

  return missing
}
