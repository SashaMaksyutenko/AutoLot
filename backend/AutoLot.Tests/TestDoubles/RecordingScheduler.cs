using AutoLot.Application.Auctions;

namespace AutoLot.Tests.TestDoubles;

/// <summary>
/// Планувальник, який нічого не планує, а лише записує замовлення. Так тест
/// може перевірити, що після антиснайпінгу задачу переставили на новий час,
/// не піднімаючи справжнього Quartz і не чекаючи реального спрацювання.
/// </summary>
internal sealed class RecordingScheduler : IAuctionScheduler
{
    private readonly List<(long ListingId, DateTimeOffset EndsAt)> orders = [];

    public IReadOnlyList<(long ListingId, DateTimeOffset EndsAt)> Orders => orders;

    public Task ScheduleCloseAsync(
        long listingId,
        DateTimeOffset endsAt,
        CancellationToken cancellationToken = default)
    {
        lock (orders)
        {
            orders.Add((listingId, endsAt));
        }

        return Task.CompletedTask;
    }
}
