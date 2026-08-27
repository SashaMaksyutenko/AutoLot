import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  answerQuestion,
  askQuestion,
  fetchQuestions,
  type QuestionRecord,
} from '../../api/questions'
import { ApiError } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { openSignIn } from '../../auth/signInPrompt'
import { formatDateTime } from '../../format'

interface Props {
  listingId: number
  /** Чи дивиться сторінку сам продавець — тоді замість форми питання буде форма відповіді. */
  isSeller: boolean
}

/**
 * Публічні питання під лотом (SPEC §4). Це не приватний чат: питання й
 * відповідь бачить кожен і вони стають частиною опису авто. Для аукціону це
 * критично — покупець не оглядає машину особисто, а відповідь одному майже
 * завжди цікавить усіх, хто торгується.
 */
export function Questions({ listingId, isSeller }: Props) {
  const auth = useAuth()
  const queryClient = useQueryClient()

  const questions = useQuery({
    queryKey: ['questions', listingId],
    queryFn: ({ signal }) => fetchQuestions(listingId, signal),
  })

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['questions', listingId] })

  return (
    <section className="card p-4">
      <h2 className="eyebrow mb-3">
        Питання продавцю{questions.data?.length ? ` · ${questions.data.length}` : ''}
      </h2>

      {/* Продавцю форму питання не показуємо: питати самого себе немає сенсу. */}
      {!isSeller && (
        <AskForm
          listingId={listingId}
          isAuthenticated={auth.user !== null}
          onAsked={refresh}
        />
      )}

      {questions.isPending && <p className="text-sm text-ink-2">Завантажуємо…</p>}

      {questions.data?.length === 0 && (
        <p className="text-sm text-ink-2">
          Питань поки немає.{isSeller ? '' : ' Запитайте перші — відповідь побачать усі.'}
        </p>
      )}

      <div className="grid gap-3">
        {questions.data?.map((question) => (
          <QuestionItem
            key={question.id}
            listingId={listingId}
            question={question}
            isSeller={isSeller}
            onAnswered={refresh}
          />
        ))}
      </div>
    </section>
  )
}

function AskForm({
  listingId,
  isAuthenticated,
  onAsked,
}: {
  listingId: number
  isAuthenticated: boolean
  onAsked: () => void
}) {
  const [text, setText] = useState('')
  const [error, setError] = useState<string | null>(null)

  const ask = useMutation({
    mutationFn: () => askQuestion(listingId, text),
    onSuccess: () => {
      setText('')
      setError(null)
      onAsked()
    },
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : 'Не вдалося надіслати питання.'),
  })

  if (!isAuthenticated) {
    return (
      <p className="mb-4 rounded-control bg-surface-2 px-3 py-2 text-[13px] text-ink-2">
        <button type="button" onClick={openSignIn} className="text-accent hover:underline">
          Увійдіть
        </button>
        , щоб поставити запитання продавцю.
      </p>
    )
  }

  return (
    <form
      className="mb-4 grid gap-2"
      onSubmit={(event) => {
        event.preventDefault()
        ask.mutate()
      }}
    >
      <textarea
        value={text}
        onChange={(event) => setText(event.target.value)}
        rows={2}
        maxLength={1000}
        placeholder="Наприклад: чи фарбувалися елементи кузова?"
        className="control resize-y"
      />

      {error && <p className="text-[12px] text-danger">{error}</p>}

      <div className="flex items-center justify-between gap-3">
        <span className="text-[12px] text-ink-3">Питання й відповідь побачать усі</span>
        <button
          type="submit"
          disabled={ask.isPending || text.trim().length < 5}
          className="btn btn-primary"
        >
          {ask.isPending ? 'Надсилаємо…' : 'Запитати'}
        </button>
      </div>
    </form>
  )
}

function QuestionItem({
  listingId,
  question,
  isSeller,
  onAnswered,
}: {
  listingId: number
  question: QuestionRecord
  isSeller: boolean
  onAnswered: () => void
}) {
  return (
    <article className="border-t border-line pt-3 first:border-0 first:pt-0">
      <div className="flex items-baseline justify-between gap-3">
        <span className="text-[13.5px] font-semibold">{question.askerName}</span>
        <span className="text-[11.5px] text-ink-3">{formatDateTime(question.createdAt)}</span>
      </div>

      <p className="mt-1 text-[14px] whitespace-pre-line">{question.text}</p>

      {question.answer ? (
        // Відповідь із відступом і смугою — щоб очима одразу було видно, де
        // питання, а де відповідь продавця.
        <div className="mt-2 border-l-2 border-accent pl-3">
          <div className="flex items-baseline justify-between gap-3">
            <span className="text-[12px] font-semibold text-accent">Відповідь продавця</span>
            {question.answeredAt && (
              <span className="text-[11.5px] text-ink-3">
                {formatDateTime(question.answeredAt)}
              </span>
            )}
          </div>
          <p className="mt-0.5 text-[14px] whitespace-pre-line text-ink-2">{question.answer}</p>
        </div>
      ) : (
        <p className="mt-1 text-[12px] text-ink-3">Продавець ще не відповів</p>
      )}

      {isSeller && (
        <AnswerForm
          listingId={listingId}
          question={question}
          onAnswered={onAnswered}
        />
      )}
    </article>
  )
}

function AnswerForm({
  listingId,
  question,
  onAnswered,
}: {
  listingId: number
  question: QuestionRecord
  onAnswered: () => void
}) {
  // Уже відповів — форму згортаємо, поки не натиснуть «виправити»: інакше під
  // кожним питанням висіло б порожнє поле.
  const [isOpen, setOpen] = useState(question.answer === null)
  const [text, setText] = useState(question.answer ?? '')
  const [error, setError] = useState<string | null>(null)

  const answer = useMutation({
    mutationFn: () => answerQuestion(listingId, question.id, text),
    onSuccess: () => {
      setError(null)
      setOpen(false)
      onAnswered()
    },
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : 'Не вдалося зберегти відповідь.'),
  })

  if (!isOpen) {
    return (
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="mt-2 text-[12px] text-accent hover:underline"
      >
        Виправити відповідь
      </button>
    )
  }

  return (
    <form
      className="mt-2 grid gap-2"
      onSubmit={(event) => {
        event.preventDefault()
        answer.mutate()
      }}
    >
      <textarea
        value={text}
        onChange={(event) => setText(event.target.value)}
        rows={2}
        maxLength={2000}
        placeholder="Ваша відповідь"
        className="control resize-y"
      />

      {error && <p className="text-[12px] text-danger">{error}</p>}

      <div className="flex gap-2">
        <button
          type="submit"
          disabled={answer.isPending || text.trim().length === 0}
          className="btn btn-primary"
        >
          {answer.isPending ? 'Зберігаємо…' : 'Відповісти'}
        </button>
        {question.answer !== null && (
          <button type="button" onClick={() => setOpen(false)} className="btn">
            Скасувати
          </button>
        )}
      </div>
    </form>
  )
}
