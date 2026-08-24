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
      <div className="card grid aspect-[16/10] place-items-center text-sm text-ink-3">
        Фотографій немає
      </div>
    )
  }

  // Захист від виходу за межі: список фото може прийти коротшим, ніж був,
  // а обраний номер лишиться старим.
  const active = photos[Math.min(current, photos.length - 1)]

  return (
    <div className="grid gap-2">
      <div className="relative overflow-hidden rounded-card border border-line bg-surface-2">
        <img src={`/media/${active.path}`} alt={alt} className="aspect-[16/10] w-full object-cover" />
        <span className="absolute right-2 bottom-2 rounded-[4px] bg-black/65 px-1.5 py-px font-mono text-[10.5px] text-white tabular-nums">
          {current + 1} / {photos.length}
        </span>
      </div>

      {photos.length > 1 && (
        <div className="grid grid-cols-5 gap-2">
          {photos.map((photo, index) => (
            <button
              key={photo.id}
              type="button"
              onClick={() => setCurrent(index)}
              aria-label={`Фото ${index + 1}`}
              // outline, а не border: обведення малюється поверх і не змінює
              // розміру кнопки, тож мініатюри не стрибають при виборі.
              className={`aspect-[4/3] overflow-hidden rounded-[5px] border border-line ${
                index === current ? 'outline-2 outline-offset-1 outline-accent' : 'opacity-75'
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
