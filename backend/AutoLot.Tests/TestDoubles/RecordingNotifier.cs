using AutoLot.Application.Auctions;
using AutoLot.Application.Auctions.Dtos;

namespace AutoLot.Tests.TestDoubles;

/// <summary>
/// Сповіщувач, який нікуди нічого не шле, а просто запам'ятовує, що йому
/// передали. Так тест може перевірити, ЩО саме пішло б глядачам, не піднімаючи
/// ані вебсервера, ані з'єднання SignalR.
/// </summary>
internal sealed class RecordingNotifier : IAuctionNotifier
{
    private readonly List<AuctionUpdate> updates = [];

    public IReadOnlyList<AuctionUpdate> Updates => updates;

    public AuctionUpdate? Last => updates.Count > 0 ? updates[^1] : null;

    public Task BidPlacedAsync(AuctionUpdate update, CancellationToken cancellationToken = default)
    {
        // Блокування, бо в тесті конкурентності сюди пишуть з півсотні потоків
        // одночасно, а List такого не терпить — може мовчки зіпсувати вміст.
        lock (updates)
        {
            updates.Add(update);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Сповіщувач, який завжди падає. Потрібен, щоб довести головне: збій
/// розсилки не має скасовувати вже прийняту ставку.
/// </summary>
internal sealed class FailingNotifier : IAuctionNotifier
{
    public Task BidPlacedAsync(AuctionUpdate update, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Канал недоступний.");
    }
}
