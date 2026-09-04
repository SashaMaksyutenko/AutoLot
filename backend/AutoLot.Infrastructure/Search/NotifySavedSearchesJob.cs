using AutoLot.Application.Search;
using Quartz;

namespace AutoLot.Infrastructure.Search;

/// <summary>
/// Задача розсилки про нові збіги. Сама нічого не вирішує — передає роботу
/// в <see cref="ISavedSearchNotifier"/>, щоб уся логіка лишалася в одному
/// місці й перевірялася тестами без Quartz.
///
/// [DisallowConcurrentExecution] тут важливіший, ніж може здатися: без нього
/// довгий прохід міг би накластися на наступний за розкладом, і людина
/// отримала б два однакові листи.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class NotifySavedSearchesJob(ISavedSearchNotifier notifier) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        await notifier.NotifyAsync(context.CancellationToken);
    }
}
