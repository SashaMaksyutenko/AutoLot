import { useState } from 'react'
import type { ListingPhoto } from '../../api/listing'

/**
 * Галерея: велике фото й стрічка мініатюр. Мініатюри тягнуть окремий
 * зменшений файл, а не той самий великий у маленькій рамці — інакше сторінка
 * вантажила б десяток повнорозмірних зображень заради смужки внизу.
 */
export function Gallery({ photos, alt }: { photos: ListingPhoto[]; alt: string }) {
  const [current, setCurrent] = useState(0)

  if (photos.length === 0) {
    return (
      <div className="flex h-[440px] items-center justify-center rounded-md border border-line bg-surface text-sm text-faint">
        Фотографій немає
      </div>
    )
  }

  const active = photos[Math.min(current, photos.length - 1)]

  return (
    <div className="flex flex-col gap-2">
      <div className="relative overflow-hidden rounded-md bg-[#2f3e52]">
        <img
          src={`/media/${active.path}`}
          alt={alt}
          className="h-[440px] w-full object-cover"
        />
        <span className="absolute right-3 bottom-3 rounded-sm bg-black/45 px-2 py-1 font-mono text-xs text-white">
          {current + 1} / {photos.length}
        </span>
      </div>

      {photos.length > 1 && (
        <div className="flex gap-2 overflow-x-auto pb-1">
          {photos.map((photo, index) => (
            <button
              key={photo.id}
              type="button"
              onClick={() => setCurrent(index)}
              aria-label={`Фото ${index + 1}`}
              className={`h-[68px] w-[100px] shrink-0 overflow-hidden rounded-sm border-2 ${
                index === current ? 'border-brand' : 'border-transparent opacity-70'
              }`}
            >
              <img
                src={`/media/${photo.thumbnailPath}`}
                alt=""
                className="h-full w-full object-cover"
                loading="lazy"
              />
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
