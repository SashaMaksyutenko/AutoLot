using AutoLot.Application.Auctions;
using AutoLot.Application.Common.Abstractions;
using Microsoft.Extensions.Logging;
using Quartz;

namespace AutoLot.Infrastructure.Auctions;

/// <summary>
/// Планувальник закриття торгів на Quartz.
///
/// Кожен лот дістає задачу з власним ключем, зібраним із його номера. Завдяки
/// цьому перепланування — це просто повторне замовлення: Quartz замінює
/// задачу з тим самим ключем, і після антиснайпінгу не лишається старої, яка
/// закрила б торги на хвилину раніше.
/// </summary>
internal sealed partial class QuartzAuctionScheduler(
    ISchedulerFactory schedulerFactory,
    IDateTimeProvider clock,
    ILogger<QuartzAuctionScheduler> logger) : IAuctionScheduler
{
    public async Task ScheduleCloseAsync(
        long listingId,
        DateTimeOffset endsAt,
        CancellationToken cancellationToken = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);

        var job = JobBuilder.Create<CloseAuctionJob>()
            .WithIdentity(KeyFor(listingId))
            .UsingJobData(CloseAuctionJob.ListingIdKey, listingId)
            .Build();

        // Час у минулому Quartz сприймає як «виконати негайно», і це саме те,
        // що треба: лот, чий строк уже минув, має закритися одразу.
        var startAt = endsAt > clock.UtcNow ? endsAt : clock.UtcNow;

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"{KeyFor(listingId)}-trigger")
            .StartAt(startAt)
            .Build();

        // replace: true — сенс усього класу. Без нього повторне замовлення
        // впало б із помилкою «така задача вже є», і антиснайпінг не працював би.
        await scheduler.ScheduleJob(job, [trigger], replace: true, cancellationToken);

        LogScheduled(logger, listingId, startAt);
    }

    private static string KeyFor(long listingId) => $"close-auction-{listingId}";

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Закриття лота {ListingId} заплановано на {StartAt}.")]
    private static partial void LogScheduled(ILogger logger, long listingId, DateTimeOffset startAt);
}
