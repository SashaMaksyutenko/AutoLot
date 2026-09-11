import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError } from '../../api/client'
import { fetchBotLinks, fetchTelegramBot, issueLinkCode } from '../../api/bots'
import { useTranslation } from '../../i18n/useTranslation'

/**
 * Прив'язка телеграм-бота.
 *
 * Картка показується лише коли бот справді піднятий: без токена в
 * налаштуваннях його немає, і розповідати про сповіщення, яких не буде, —
 * гірше, ніж промовчати.
 */
export function TelegramCard() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const bot = useQuery({
    queryKey: ['telegram-bot'],
    queryFn: ({ signal }) => fetchTelegramBot(signal),

    // Ім'я бота не змінюється без перезапуску сервера.
    staleTime: Infinity,
  })

  const links = useQuery({
    queryKey: ['bot-links'],
    queryFn: ({ signal }) => fetchBotLinks(signal),
    enabled: bot.data?.enabled === true,
  })

  const issue = useMutation({
    mutationFn: issueLinkCode,

    // Прив'язка могла відбутися, поки код був на екрані, — перепитуємо стан.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['bot-links'] }),
  })

  if (!bot.data?.enabled) return null

  const linked = links.data?.includes('Telegram') === true

  return (
    <section className="card grid gap-2 p-5">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="eyebrow">{t('bot.title')}</h2>

        {linked && <span className="pill pill-good">{t('bot.linked')}</span>}
      </div>

      <p className="text-[12.5px] text-ink-3">{t('bot.lead')}</p>

      {bot.data.username && (
        <a
          href={`https://t.me/${bot.data.username}`}
          target="_blank"
          rel="noreferrer"
          className="justify-self-start text-[13px] text-accent hover:underline"
        >
          @{bot.data.username}
        </a>
      )}

      {linked ? (
        <p className="text-[12.5px] text-ink-3">{t('bot.unlinkHint')}</p>
      ) : (
        <>
          {issue.data ? (
            <div className="grid gap-1.5">
              {/*
                Код великий і моноширинний: його переписують з екрана в
                телефон, і цифри мають читатися без зусиль.
              */}
              <span className="font-mono text-[26px] font-bold tracking-[0.2em] tabular-nums">
                {issue.data.code}
              </span>

              <p className="text-[12.5px] text-ink-2">{t('bot.howTo', { code: issue.data.code })}</p>
              <p className="text-[12px] text-ink-3">{t('bot.expiry')}</p>
            </div>
          ) : (
            <button
              type="button"
              onClick={() => issue.mutate()}
              disabled={issue.isPending}
              className="btn btn-primary justify-self-start"
            >
              {issue.isPending ? t('bot.issuing') : t('bot.getCode')}
            </button>
          )}

          {issue.isError && (
            <p className="rounded-control bg-danger-soft px-3 py-2 text-[13px] text-danger">
              {issue.error instanceof ApiError ? issue.error.message : t('error.network')}
            </p>
          )}
        </>
      )}
    </section>
  )
}
