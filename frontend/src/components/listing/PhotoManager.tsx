import { useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError } from '../../api/client'
import type { ListingPhoto } from '../../api/listing'
import {
  deletePhoto,
  fetchPhotos,
  maxPhotoBytes,
  maxPhotos,
  reorderPhotos,
  setPrimaryPhoto,
  uploadPhoto,
} from '../../api/listingPhotos'
import { useTranslation } from '../../i18n/useTranslation'

/**
 * Керування фотографіями оголошення.
 *
 * Порядок міняють стрілками, а не перетягуванням. Перетягування виглядає
 * сучасніше, але воно недоступне з клавіатури, погано працює на дотик і
 * тягне за собою бібліотеку. Дві стрілки на картці роблять те саме й
 * зрозумілі одразу.
 *
 * Головне фото не окрема сутність, а прапорець на одному зі знімків: саме
 * воно стоїть на картці авто у видачі.
 */
export function PhotoManager({ listingId }: { listingId: number }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const input = useRef<HTMLInputElement>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const photos = useQuery({
    queryKey: ['listing-photos', listingId],
    queryFn: ({ signal }) => fetchPhotos(listingId, signal),
  })

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['listing-photos', listingId] })

  const items = photos.data ?? []
  const room = maxPhotos - items.length

  /**
   * Надсилаємо файли ПО ЧЕРЗІ, а не всі разом. Сервер визначає порядок за
   * тим, у якій послідовності фото прийшли, — а паралельні запити приходять
   * у довільній. Черга повільніша, зате порядок той, який людина обрала.
   */
  async function upload(files: FileList) {
    setError(null)
    setBusy(true)

    try {
      for (const file of Array.from(files).slice(0, room)) {
        if (file.size > maxPhotoBytes) {
          setError(t('photos.tooLarge', { name: file.name }))
          continue
        }

        await uploadPhoto(listingId, file)
      }

      await refresh()
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : t('error.network'))
    } finally {
      setBusy(false)

      // Скидаємо поле: без цього повторний вибір ТОГО САМОГО файлу не
      // спрацював би — браузер вважає, що значення не змінилося.
      if (input.current) input.current.value = ''
    }
  }

  const move = useMutation({
    mutationFn: (order: number[]) => reorderPhotos(listingId, order),
    onSuccess: refresh,
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : t('error.network')),
  })

  const remove = useMutation({
    mutationFn: (photoId: number) => deletePhoto(listingId, photoId),
    onSuccess: refresh,
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : t('error.network')),
  })

  const promote = useMutation({
    mutationFn: (photoId: number) => setPrimaryPhoto(listingId, photoId),
    onSuccess: refresh,
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : t('error.network')),
  })

  /** Міняє фото місцями з сусідом і надсилає повний порядок. */
  function swap(index: number, offset: number) {
    const next = [...items]
    const neighbour = index + offset

    if (neighbour < 0 || neighbour >= next.length) return

    ;[next[index], next[neighbour]] = [next[neighbour], next[index]]

    move.mutate(next.map((photo) => photo.id))
  }

  return (
    <section className="card grid gap-3 p-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="eyebrow">{t('photos.title')}</h2>
        <span className="text-[12px] text-ink-3">
          {t('photos.counter', { used: items.length, max: maxPhotos })}
        </span>
      </div>

      {items.length === 0 ? (
        <p className="text-[13px] text-ink-2">{t('photos.empty')}</p>
      ) : (
        <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
          {items.map((photo, index) => (
            <Tile
              key={photo.id}
              photo={photo}
              index={index}
              total={items.length}
              disabled={busy || move.isPending || remove.isPending || promote.isPending}
              onFirst={() => swap(index, -1)}
              onLast={() => swap(index, 1)}
              onPrimary={() => promote.mutate(photo.id)}
              onRemove={() => remove.mutate(photo.id)}
            />
          ))}
        </ul>
      )}

      {error && (
        <p className="rounded-control bg-danger-soft px-3 py-2 text-[13px] text-danger">{error}</p>
      )}

      <div className="flex flex-wrap items-center gap-2">
        {/*
          Справжнє поле вибору файлів приховане, а натискання переадресовує
          на нього кнопка: вигляд системного «Browse…» неможливо привести до
          решти оформлення, і в кожному браузері він свій.
        */}
        <input
          ref={input}
          type="file"
          accept="image/*"
          multiple
          hidden
          onChange={(event) => {
            if (event.target.files?.length) void upload(event.target.files)
          }}
        />

        <button
          type="button"
          onClick={() => input.current?.click()}
          disabled={busy || room === 0}
          className="btn btn-primary"
        >
          {busy ? t('photos.uploading') : t('photos.add')}
        </button>

        <span className="text-[12px] text-ink-3">
          {room === 0 ? t('photos.full') : t('photos.hint')}
        </span>
      </div>
    </section>
  )
}

function Tile({
  photo,
  index,
  total,
  disabled,
  onFirst,
  onLast,
  onPrimary,
  onRemove,
}: {
  photo: ListingPhoto
  index: number
  total: number
  disabled: boolean
  onFirst: () => void
  onLast: () => void
  onPrimary: () => void
  onRemove: () => void
}) {
  const { t } = useTranslation()

  return (
    <li className="grid gap-1.5">
      <div className="relative aspect-[4/3] overflow-hidden rounded-control border border-line bg-surface-2">
        <img
          src={`/media/${photo.thumbnailPath}`}
          alt={t('photos.alt', { index: index + 1 })}
          className="h-full w-full object-cover"
          loading="lazy"
        />

        {photo.isPrimary && (
          <span className="pill pill-accent absolute top-1.5 left-1.5">{t('photos.primary')}</span>
        )}
      </div>

      <div className="flex items-center gap-1">
        <Small
          label="←"
          title={t('photos.moveEarlier')}
          disabled={disabled || index === 0}
          onClick={onFirst}
        />
        <Small
          label="→"
          title={t('photos.moveLater')}
          disabled={disabled || index === total - 1}
          onClick={onLast}
        />

        {!photo.isPrimary && (
          <button
            type="button"
            onClick={onPrimary}
            disabled={disabled}
            className="flex-1 text-[12px] text-accent hover:underline"
          >
            {t('photos.makePrimary')}
          </button>
        )}

        <button
          type="button"
          onClick={onRemove}
          disabled={disabled}
          title={t('photos.remove')}
          aria-label={t('photos.remove')}
          className="ml-auto px-1 text-[13px] text-ink-3 hover:text-danger"
        >
          ×
        </button>
      </div>
    </li>
  )
}

function Small({
  label,
  title,
  disabled,
  onClick,
}: {
  label: string
  title: string
  disabled: boolean
  onClick: () => void
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      title={title}
      aria-label={title}
      className="grid h-[22px] w-[22px] place-items-center rounded-control border border-line text-[12px] text-ink-2 hover:text-ink disabled:opacity-40"
    >
      {label}
    </button>
  )
}
