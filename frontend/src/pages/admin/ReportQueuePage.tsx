import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { fetchReportQueue, resolveReport, type ReportSummary } from '../../api/reports'
import { ApiError } from '../../api/client'
import { formatDateTime, plural } from '../../format'

/**
 * Черга скарг. Окрема від черги модерації, і це не дублювання: там рішення
 * «чи пускати», тут — «чи знімати вже опубліковане». Робота різна, і плутати
 * їх в одному списку означало б плутати два різні наміри модератора.
 */
export function ReportQueuePage() {
  const queryClient = useQueryClient()

  const queue = useQuery({
    queryKey: ['report-queue'],
    queryFn: ({ signal }) => fetchReportQueue(signal),
  })

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['report-queue'] })
    void queryClient.invalidateQueries({ queryKey: ['admin-stats'] })
  }

  const count = queue.data?.length ?? 0

  return (
    <>
      <div>
        <h1 className="font-display text-[25px] font-bold">Скарги</h1>
        <p className="text-[13px] text-ink-2">
          {queue.isPending
            ? 'Завантажуємо…'
            : `${count} ${plural(count, 'скарга чекає', 'скарги чекають', 'скарг чекають')}`}
        </p>
      </div>

      {count === 0 && !queue.isPending && (
        <p className="card p-10 text-center text-sm text-ink-2">
          Скарг немає — на майданчику тихо.
        </p>
      )}

      <div className="grid gap-3">
        {queue.data?.map((report) => (
          <ReportRow key={report.id} report={report} onDecided={refresh} />
        ))}
      </div>
    </>
  )
}

function ReportRow({ report, onDecided }: { report: ReportSummary; onDecided: () => void }) {
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)

  const decide = useMutation({
    mutationFn: (accepted: boolean) => resolveReport(report.id, accepted, note),
    onSuccess: onDecided,
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : 'Не вдалося зберегти рішення.'),
  })

  return (
    <article className="card grid gap-3 p-3">
      <div className="flex flex-wrap items-start gap-3">
        <Thumbnail path={report.listingPhoto} />

        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <span className="pill pill-live">{report.reasonName}</span>

            {/* Кілька скарг на один лот — це вже не суперечка смаків. */}
            {report.otherPendingForListing > 0 && (
              <span className="pill">
                ще {report.otherPendingForListing}{' '}
                {plural(report.otherPendingForListing, 'скарга', 'скарги', 'скарг')} на це авто
              </span>
            )}
          </div>

          {/* Рішення без огляду самого оголошення неможливе. */}
          <Link
            to={`/listing/${report.listingId}`}
            target="_blank"
            className="font-display text-[15.5px] font-semibold hover:text-accent"
          >
            {report.listingTitle}
          </Link>

          <p className="text-[12.5px] text-ink-2">
            {report.reporterName} · {formatDateTime(report.createdAt)}
          </p>

          {report.comment && (
            <p className="mt-1 rounded-control bg-surface-2 px-2.5 py-2 text-[13px] whitespace-pre-line">
              {report.comment}
            </p>
          )}
        </div>
      </div>

      <div className="grid gap-2 border-t border-line pt-3">
        <label className="grid gap-1">
          <span className="text-[11.5px] font-semibold text-ink-2">
            Нотатка — її бачать лише модератори
          </span>
          <input
            value={note}
            onChange={(event) => setNote(event.target.value)}
            maxLength={1000}
            placeholder="Наприклад: перевірив VIN, дані збігаються"
            className="control"
          />
        </label>

        {error && <p className="text-[12px] text-danger">{error}</p>}

        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={() => {
              setError(null)
              decide.mutate(true)
            }}
            disabled={decide.isPending}
            className="btn btn-signal"
          >
            {decide.isPending ? 'Зберігаємо…' : 'Зняти оголошення'}
          </button>

          <button
            type="button"
            onClick={() => {
              setError(null)
              decide.mutate(false)
            }}
            disabled={decide.isPending}
            className="btn"
          >
            Порушення немає
          </button>
        </div>
      </div>
    </article>
  )
}

function Thumbnail({ path }: { path: string | null }) {
  if (!path) {
    return (
      <span className="grid h-[60px] w-[80px] shrink-0 place-items-center rounded-control border border-line bg-surface-2 text-[11px] text-ink-3">
        без фото
      </span>
    )
  }

  return (
    <img
      src={`/media/${path}`}
      alt=""
      className="h-[60px] w-[80px] shrink-0 rounded-control border border-line object-cover"
    />
  )
}
