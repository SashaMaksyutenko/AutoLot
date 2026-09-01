import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { fetchReportReasons, submitReport, type ReportReason } from '../../api/reports'
import { ApiError } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { openSignIn } from '../../auth/signInPrompt'

/**
 * «Поскаржитися на оголошення».
 *
 * Свідомо тиха, дрібна й унизу: скарга — рідкісна дія, а помітна червона
 * кнопка спокушала б тиснути її замість «написати продавцю». Той, кому вона
 * справді потрібна, знайде її й такою.
 *
 * Що сталося зі скаргою далі, тут не показуємо взагалі: рішення бачить лише
 * модератор. Інакше скаржник знав би, коли його сигнал відхилили, і почав би
 * надсилати його знову.
 */
export function ReportButton({ listingId }: { listingId: number }) {
  const auth = useAuth()
  const [open, setOpen] = useState(false)

  if (!auth.user) {
    return (
      <Trigger onClick={openSignIn} label="Поскаржитися на оголошення" />
    )
  }

  if (!open) {
    return (
      <Trigger onClick={() => setOpen(true)} label="Поскаржитися на оголошення" />
    )
  }

  return <ReportForm listingId={listingId} onClose={() => setOpen(false)} />
}

/**
 * Роздільник згори відділяє скаргу від даних продавця: це дія іншого роду,
 * і вона не має читатися продовженням адреси. Прапорець потрібен, щоб око
 * знаходило рядок — самим лише дрібним сірим текстом його не видно.
 */
function Trigger({ onClick, label }: { onClick: () => void; label: string }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="flex items-center gap-1.5 border-t border-line pt-3 text-left text-[12.5px] text-ink-2 underline-offset-2 hover:text-danger hover:underline"
    >
      <FlagIcon />
      {label}
    </button>
  )
}

/**
 * Прапорець — усталений знак скарги. Малюємо розміткою, а не картинкою:
 * так він успадковує колір тексту й сам змінюється при наведенні.
 */
function FlagIcon() {
  return (
    <svg
      viewBox="0 0 16 16"
      width="13"
      height="13"
      aria-hidden="true"
      className="shrink-0"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.6"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M3.5 14.5V2" />
      <path d="M3.5 2.8h8.2l-1.6 3 1.6 3H3.5" />
    </svg>
  )
}

function ReportForm({ listingId, onClose }: { listingId: number; onClose: () => void }) {
  const [reason, setReason] = useState<ReportReason>('Fraud')
  const [comment, setComment] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState<string | null>(null)

  // Причини приходять із сервера вже перекладеними, як і решта довідників.
  // Кешуємо назавжди: за час сеансу вони не змінюються.
  const reasons = useQuery({
    queryKey: ['report-reasons'],
    queryFn: ({ signal }) => fetchReportReasons(signal),
    staleTime: Infinity,
  })

  const send = useMutation({
    mutationFn: () => submitReport(listingId, reason, comment.trim()),
    onSuccess: (receipt) =>
      setDone(
        receipt.isNew
          ? 'Дякуємо, скаргу передано модератору.'
          : 'Ви вже скаржилися на це оголошення — скарга в черзі.',
      ),
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : 'Не вдалося надіслати скаргу.'),
  })

  if (done) {
    return (
      <div className="card grid gap-2 p-3">
        <p className="text-[13px]">{done}</p>
        <button type="button" onClick={onClose} className="btn justify-self-start">
          Закрити
        </button>
      </div>
    )
  }

  // Пояснення обов'язкове лише для «інше»: решта причин говорять самі за себе,
  // а зайве поле відлякує тих, хто справді хоче поскаржитися. Те саме правило
  // перевіряє й сервер — тут воно лише заради зрозумілої форми.
  const needsComment = reason === 'Other'
  const canSend = !send.isPending && (!needsComment || comment.trim().length > 0)

  return (
    <form
      className="card grid gap-2 p-3"
      onSubmit={(event) => {
        event.preventDefault()
        setError(null)
        send.mutate()
      }}
    >
      <span className="eyebrow">Що не так з оголошенням?</span>

      <select
        value={reason}
        onChange={(event) => setReason(event.target.value as ReportReason)}
        className="control"
      >
        {reasons.data?.map((item) => (
          <option key={item.value} value={item.value}>
            {item.name}
          </option>
        ))}
      </select>

      <textarea
        value={comment}
        onChange={(event) => setComment(event.target.value)}
        rows={3}
        maxLength={1000}
        placeholder={needsComment ? 'Опишіть, у чому річ' : 'Подробиці (не обов’язково)'}
        className="control resize-y"
      />

      {error && <p className="text-[12px] text-danger">{error}</p>}

      <div className="flex gap-2">
        <button type="submit" disabled={!canSend} className="btn btn-primary">
          {send.isPending ? 'Надсилаємо…' : 'Надіслати'}
        </button>
        <button type="button" onClick={onClose} className="btn">
          Скасувати
        </button>
      </div>
    </form>
  )
}
