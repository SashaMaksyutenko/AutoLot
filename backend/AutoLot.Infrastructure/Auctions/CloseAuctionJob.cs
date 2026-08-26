using AutoLot.Application.Auctions;
using Quartz;

namespace AutoLot.Infrastructure.Auctions;

/// <summary>
/// Задача, яку планувальник запускає в мить завершення торгів.
///
/// Сама вона нічого не вирішує — лише передає номер лота в
/// <see cref="IAuctionCloser"/>. Так уся логіка лишається в одному місці й
/// перевіряється тестами без Quartz.
///
/// [DisallowConcurrentExecution] забороняє двом копіям цієї задачі йти
/// одночасно в межах одного процесу. Від кількох СЕРВЕРІВ це не рятує — там
/// працює блокування рядка в базі, — але зайвих спроб усе одно менше.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class CloseAuctionJob(IAuctionCloser closer) : IJob
{
    /// <summary>Ключ, під яким у задачу кладеться номер лота.</summary>
    public const string ListingIdKey = "listingId";

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var listingId = context.MergedJobDataMap.GetLong(ListingIdKey);

        await closer.CloseAsync(listingId, context.CancellationToken);
    }
}
