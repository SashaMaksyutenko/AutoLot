import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import {
  commentMaxLength,
  fetchComments,
  postComment,
  type AuctionComment,
} from '../../api/auctionComments'
import { watchAuction } from '../../api/auctionHub'
import { ApiError } from '../../api/client'
import { openSignIn } from '../../auth/signInPrompt'
import { useAuth } from '../../auth/useAuth'
import { formatDateTime } from '../../format'
import { useTranslation } from '../../i18n/useTranslation'

const keyOf = (listingId: number) => ['auction-comments', listingId]

/**
 * Кладе коментар на початок уже завантаженого списку — якщо його там ще немає.
 *
 * Перевірка на дубль потрібна для ВЛАСНОГО коментаря: його ми додаємо самі,
 * щойно сервер прийняв, — і за мить той самий рядок повертається живим каналом.
 */
function remember(queryClient: QueryClient, listingId: number, comment: AuctionComment) {
  queryClient.setQueryData<AuctionComment[]>(keyOf(listingId), (current = []) =>
    current.some((existing) => existing.id === comment.id) ? current : [comment, ...current],
  )
}

/**
 * Жива розмова під лотом.
 *
 * Не те саме, що питання продавцю. Питання — розмова «покупець → продавець»
 * з однією відповіддю. Тут пишуть усі всім і прямо під час торгів: «на
 * третьому фото іржа на порозі», «такий мотор ходить 400 тисяч». Саме це й
 * робить торги живими, а не переліком цифр.
 */
export function AuctionComments({ listingId, open }: { listingId: number; open: boolean }) {
  const { t } = useTranslation()
  const auth = useAuth()
  const queryClient = useQueryClient()

  // Торги можуть скінчитися, поки сторінка відкрита. Тоді ховаємо форму
  // одразу, щоб людина не писала коментар, який сервер уже не прийме.
  const [ended, setEnded] = useState(false)
  const writable = open && !ended

  const comments = useQuery({
    queryKey: keyOf(listingId),
    queryFn: ({ signal }) => fetchComments(listingId, signal),
  })

  /*
    Новий коментар від будь-кого приходить живим каналом. Кладемо його прямо
    в кеш запиту, а не просимо сервер переслати все наново: список уже в нас,
    бракує лише одного рядка.
  */
  useEffect(() => {
    return watchAuction(listingId, {
      onEnded: () => setEnded(true),
      onComment: (comment) => remember(queryClient, listingId, comment),
    })
  }, [listingId, queryClient])

  const items = comments.data ?? []

  // Після фіналу розмову лишаємо на видноті, але писати вже нікому.
  if (!writable && items.length === 0) return null

  return (
    <section className="card grid gap-3 p-4">
      <h2 className="font-display text-[17px] font-bold">{t('comments.title')}</h2>

      {writable &&
        (auth.user ? (
          <CommentForm listingId={listingId} />
        ) : (
          <p className="text-[13px] text-ink-2">
            <button type="button" onClick={openSignIn} className="text-accent hover:underline">
              {t('account.signIn')}
            </button>{' '}
            {t('comments.signInToWrite')}
          </p>
        ))}

      {items.length === 0 ? (
        <p className="text-[13px] text-ink-3">{t('comments.empty')}</p>
      ) : (
        <ol className="grid gap-2.5">
          {items.map((comment) => (
            <li key={comment.id} className="grid gap-0.5">
              <div className="flex flex-wrap items-baseline gap-x-2 text-[12.5px]">
                <strong>{comment.authorName}</strong>

                {/* Відповідь продавця важить більше за чужу думку — не губимо її. */}
                {comment.isSeller && <span className="pill pill-accent">{t('comments.seller')}</span>}

                <span className="text-ink-3">{formatDateTime(comment.createdAt)}</span>
              </div>

              {/*
                whitespace-pre-line зберігає переноси рядків, які людина
                поставила сама, — інакше абзаци злиплися б в один.
              */}
              <p className="text-[14px] whitespace-pre-line">{comment.text}</p>
            </li>
          ))}
        </ol>
      )}
    </section>
  )
}

function CommentForm({ listingId }: { listingId: number }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [text, setText] = useState('')
  const [error, setError] = useState<string | null>(null)

  const send = useMutation({
    mutationFn: () => postComment(listingId, text),
    onSuccess: (comment) => {
      setText('')
      setError(null)

      // Свій коментар показуємо одразу, не чекаючи, поки повернеться каналом.
      remember(queryClient, listingId, comment)
    },
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : t('comments.failed')),
  })

  return (
    <form
      onSubmit={(event) => {
        // Без цього браузер перезавантажив би сторінку — так форма поводиться
        // за замовчуванням, ще з тих часів, коли JavaScript на сторінках не було.
        event.preventDefault()

        if (text.trim()) send.mutate()
      }}
      className="grid gap-2"
    >
      <textarea
        value={text}
        onChange={(event) => setText(event.target.value)}
        maxLength={commentMaxLength}
        rows={2}
        placeholder={t('comments.placeholder')}
        className="control resize-y"
      />

      <div className="flex flex-wrap items-center gap-3">
        <button type="submit" disabled={send.isPending || !text.trim()} className="btn btn-primary">
          {t('comments.send')}
        </button>

        {error && <p className="text-[12px] text-danger">{error}</p>}
      </div>
    </form>
  )
}
