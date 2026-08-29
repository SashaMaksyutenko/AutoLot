import { useState } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { fetchUsers, setUserBanned, setUserRole, type UserSummary } from '../../api/admin'
import { ApiError } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { formatCount, formatDateTime, plural } from '../../format'

/**
 * Керування людьми: пошук, блокування, ролі.
 *
 * Саме тут з'являються модератори. Раніше роль існувала, але носіїв у неї не
 * було: сід створює лише адміністратора, і кожен новий модератор вимагав би
 * правки конфігурації з перезапуском сервера.
 */
export function AdminUsersPage() {
  const [text, setText] = useState('')
  const [page, setPage] = useState(1)
  const [error, setError] = useState<string | null>(null)

  const queryClient = useQueryClient()

  const users = useQuery({
    queryKey: ['admin-users', text, page],
    queryFn: ({ signal }) => fetchUsers({ text, page }, signal),
    placeholderData: keepPreviousData,
  })

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['admin-users'] })
    void queryClient.invalidateQueries({ queryKey: ['admin-stats'] })
  }

  const onError = (caught: unknown) =>
    setError(caught instanceof ApiError ? caught.message : 'Дію не виконано.')

  return (
    <>
      <div>
        <h1 className="font-display text-[25px] font-bold">Користувачі</h1>
        <p className="text-[13px] text-ink-2">
          {users.isPending ? (
            'Завантажуємо…'
          ) : (
            <>
              <span className="font-mono font-semibold text-ink tabular-nums">
                {formatCount(users.data?.totalCount ?? 0)}
              </span>{' '}
              {plural(users.data?.totalCount ?? 0, 'людина', 'людини', 'людей')}
            </>
          )}
        </p>
      </div>

      <input
        value={text}
        onChange={(event) => {
          setText(event.target.value)
          setPage(1)
        }}
        placeholder="Ім'я або пошта"
        className="control max-w-[320px]"
      />

      {error && (
        <p className="rounded-control bg-danger-soft px-3 py-2 text-[13px] text-danger">{error}</p>
      )}

      <div className="grid gap-2">
        {users.data?.items.map((user) => (
          <UserRow key={user.id} user={user} onChanged={refresh} onError={onError} />
        ))}
      </div>

      {users.data && users.data.totalPages > 1 && (
        <nav className="mt-2 flex items-center justify-center gap-1.5">
          <button
            type="button"
            className="btn"
            disabled={!users.data.hasPrevious}
            onClick={() => setPage((current) => current - 1)}
          >
            ←
          </button>
          <span className="px-3 font-mono text-sm tabular-nums">
            {users.data.page} / {users.data.totalPages}
          </span>
          <button
            type="button"
            className="btn"
            disabled={!users.data.hasNext}
            onClick={() => setPage((current) => current + 1)}
          >
            →
          </button>
        </nav>
      )}
    </>
  )
}

function UserRow({
  user,
  onChanged,
  onError,
}: {
  user: UserSummary
  onChanged: () => void
  onError: (caught: unknown) => void
}) {
  const auth = useAuth()

  // Себе не блокують і роль адміністратора з себе не знімають — бекенд це
  // забороняє, а тут просто ховаємо кнопки, щоб людина не тицяла даремно.
  const isSelf = auth.user?.id === user.id

  const ban = useMutation({
    mutationFn: () => setUserBanned(user.id, !user.isBanned),
    onSuccess: onChanged,
    onError,
  })

  const role = useMutation({
    mutationFn: (granted: boolean) => setUserRole(user.id, 'Moderator', granted),
    onSuccess: onChanged,
    onError,
  })

  const isModerator = user.roles.includes('Moderator')
  const isAdmin = user.roles.includes('Admin')

  return (
    <article className="card flex flex-wrap items-center gap-3 p-3">
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-1.5">
          <span className="font-semibold">{user.displayName}</span>
          {isAdmin && <span className="pill pill-accent">Адмін</span>}
          {isModerator && <span className="pill pill-accent">Модератор</span>}
          {user.accountType === 'Dealer' && <span className="pill">Салон</span>}
          {user.isBanned && <span className="pill pill-danger">Заблокований</span>}
          {!user.emailConfirmed && <span className="pill">Пошта не підтверджена</span>}
        </div>

        <p className="truncate text-[12.5px] text-ink-2">{user.email}</p>

        <p className="text-[11.5px] text-ink-3">
          З {formatDateTime(user.createdAt)}
          {user.lastLoginAt ? ` · заходив ${formatDateTime(user.lastLoginAt)}` : ' · ще не заходив'}
          {user.activeListingCount > 0 ? ` · ${user.activeListingCount} оголошень` : ''}
        </p>
      </div>

      {!isSelf && (
        <div className="flex flex-wrap gap-2">
          {/* Адміністратора модератором не роблять — у нього й так усе є. */}
          {!isAdmin && (
            <button
              type="button"
              onClick={() => role.mutate(!isModerator)}
              disabled={role.isPending}
              className="btn"
            >
              {isModerator ? 'Зняти модератора' : 'Зробити модератором'}
            </button>
          )}

          <button
            type="button"
            onClick={() => ban.mutate()}
            disabled={ban.isPending}
            className={`btn ${user.isBanned ? '' : 'btn-signal'}`}
          >
            {user.isBanned ? 'Розблокувати' : 'Заблокувати'}
          </button>
        </div>
      )}
    </article>
  )
}
