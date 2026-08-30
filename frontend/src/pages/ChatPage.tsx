import { useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  fetchConversation,
  fetchConversations,
  sendMessage,
  type ConversationSummary,
  type MessageRecord,
} from '../api/chat'
import { watchChat } from '../api/chatHub'
import { ApiError } from '../api/client'
import { useAuth } from '../auth/useAuth'
import { openSignIn } from '../auth/signInPrompt'
import { formatDateTime } from '../format'

/**
 * Листування: перелік розмов ліворуч, обрана — праворуч.
 *
 * Це приватний чат, не публічні питання під лотом. Там відповідь бачить
 * кожен, бо стосується авто; тут — час огляду, торг, адреса.
 */
export function ChatPage() {
  const auth = useAuth()
  const queryClient = useQueryClient()
  const [params, setParams] = useSearchParams()

  const selectedId = Number(params.get('id')) || null

  const conversations = useQuery({
    queryKey: ['conversations'],
    queryFn: ({ signal }) => fetchConversations(signal),
    enabled: !auth.isRestoring && auth.user !== null,
  })

  // Нове повідомлення приходить саме — і оновлює обидва списки: стрічку
  // відкритої розмови й перелік із лічильниками непрочитаних.
  useEffect(() => {
    if (!auth.user) return

    return watchChat(() => {
      void queryClient.invalidateQueries({ queryKey: ['conversations'] })
      void queryClient.invalidateQueries({ queryKey: ['conversation'] })
      void queryClient.invalidateQueries({ queryKey: ['chat-unread'] })
    })
  }, [auth.user, queryClient])

  if (auth.isRestoring) {
    return <Notice>Завантажуємо…</Notice>
  }

  if (!auth.user) {
    return (
      <Notice>
        Листування доступне після входу.{' '}
        <button type="button" onClick={openSignIn} className="text-accent hover:underline">
          Увійти
        </button>
      </Notice>
    )
  }

  return (
    <div className="wrap grid items-start gap-[22px] py-[26px] lg:grid-cols-[300px_minmax(0,1fr)]">
      <aside className="card grid gap-1 self-start p-2 lg:sticky lg:top-[74px]">
        <h1 className="eyebrow px-2 pt-1 pb-2">
          Розмови
          {conversations.data?.length ? ` · ${conversations.data.length}` : ''}
        </h1>

        {conversations.data?.length === 0 && (
          <p className="px-2 pb-2 text-[13px] text-ink-2">
            Розмов поки немає. Напишіть продавцю зі сторінки авто.
          </p>
        )}

        {conversations.data?.map((conversation) => (
          <ConversationRow
            key={conversation.id}
            conversation={conversation}
            active={conversation.id === selectedId}
            onSelect={() => setParams({ id: String(conversation.id) })}
          />
        ))}
      </aside>

      {selectedId === null ? (
        <p className="card p-10 text-center text-sm text-ink-2">
          Оберіть розмову зліва.
        </p>
      ) : (
        <Thread conversationId={selectedId} viewerId={auth.user.id} />
      )}
    </div>
  )
}

function ConversationRow({
  conversation,
  active,
  onSelect,
}: {
  conversation: ConversationSummary
  active: boolean
  onSelect: () => void
}) {
  return (
    <button
      type="button"
      onClick={onSelect}
      className={`grid gap-0.5 rounded-control px-2 py-2 text-left ${
        active ? 'bg-accent-soft' : 'hover:bg-surface-2'
      }`}
    >
      <span className="flex items-center justify-between gap-2">
        <span className="truncate text-[13.5px] font-semibold">{conversation.companionName}</span>
        {conversation.unreadCount > 0 && (
          <span className="pill pill-live shrink-0 tabular-nums">{conversation.unreadCount}</span>
        )}
      </span>

      <span className="truncate text-[12px] text-ink-2">{conversation.listingTitle}</span>

      {conversation.lastMessageText && (
        <span className="truncate text-[12px] text-ink-3">{conversation.lastMessageText}</span>
      )}
    </button>
  )
}

function Thread({ conversationId, viewerId }: { conversationId: number; viewerId: number }) {
  const queryClient = useQueryClient()
  const [text, setText] = useState('')
  const [error, setError] = useState<string | null>(null)

  const conversation = useQuery({
    queryKey: ['conversation', conversationId],
    queryFn: ({ signal }) => fetchConversation(conversationId, signal),
  })

  const send = useMutation({
    mutationFn: () => sendMessage(conversationId, text),
    onSuccess: () => {
      setText('')
      setError(null)
      void queryClient.invalidateQueries({ queryKey: ['conversation', conversationId] })
      void queryClient.invalidateQueries({ queryKey: ['conversations'] })
    },
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : 'Не вдалося надіслати.'),
  })

  // Стрічка гортається донизу: у листуванні цікаве останнє, а не перше.
  const bottom = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottom.current?.scrollIntoView({ block: 'end' })
  }, [conversation.data?.messages.length])

  if (conversation.isPending) {
    return <div className="card p-10 text-center text-sm text-ink-2">Завантажуємо…</div>
  }

  if (conversation.isError || !conversation.data) {
    return <div className="card p-10 text-center text-sm text-danger">Розмову не знайдено.</div>
  }

  const data = conversation.data

  return (
    <section className="card grid gap-3 p-4">
      <header className="flex items-center justify-between gap-3 border-b border-line pb-3">
        <div className="min-w-0">
          <h2 className="font-display truncate text-[16px] font-semibold">{data.companionName}</h2>
          <Link
            to={`/listing/${data.listingId}`}
            className="truncate text-[12.5px] text-ink-2 hover:text-accent"
          >
            {data.listingTitle}
          </Link>
        </div>

        <span className="pill">{data.viewerIsSeller ? 'Ви продавець' : 'Ви покупець'}</span>
      </header>

      <div className="grid max-h-[52vh] gap-2 overflow-y-auto">
        {data.messages.length === 0 && (
          <p className="py-6 text-center text-[13px] text-ink-2">
            Повідомлень ще немає — напишіть перше.
          </p>
        )}

        {data.messages.map((message) => (
          <Bubble key={message.id} message={message} mine={message.senderId === viewerId} />
        ))}

        <div ref={bottom} />
      </div>

      <form
        className="grid gap-2 border-t border-line pt-3"
        onSubmit={(event) => {
          event.preventDefault()
          send.mutate()
        }}
      >
        <textarea
          value={text}
          onChange={(event) => setText(event.target.value)}
          rows={2}
          maxLength={4000}
          placeholder="Повідомлення"
          className="control resize-y"
        />

        {error && <p className="text-[12px] text-danger">{error}</p>}

        <button
          type="submit"
          disabled={send.isPending || text.trim().length === 0}
          className="btn btn-primary justify-self-end"
        >
          {send.isPending ? 'Надсилаємо…' : 'Надіслати'}
        </button>
      </form>
    </section>
  )
}

/** Своє праворуч і акцентом, чуже ліворуч — так видно, хто що написав. */
function Bubble({ message, mine }: { message: MessageRecord; mine: boolean }) {
  return (
    <div className={`flex ${mine ? 'justify-end' : 'justify-start'}`}>
      <div
        className={`max-w-[75%] rounded-card px-3 py-2 ${
          mine ? 'bg-accent-soft' : 'bg-surface-2'
        }`}
      >
        <p className="text-[14px] whitespace-pre-line">{message.text}</p>
        <p className="mt-1 text-[11px] text-ink-3">
          {formatDateTime(message.createdAt)}
          {mine && message.isRead ? ' · прочитано' : ''}
        </p>
      </div>
    </div>
  )
}

function Notice({ children }: { children: React.ReactNode }) {
  return (
    <div className="wrap py-16">
      <p className="card mx-auto max-w-[460px] p-10 text-center text-sm text-ink-2">{children}</p>
    </div>
  )
}
