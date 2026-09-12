using AutoLot.Application.Bots;
using AutoLot.Application.Listings.Dtos;

namespace AutoLot.Tests.TestDoubles;

/// <summary>
/// Месенджер, якого немає.
///
/// Тести аукціону й розсилки перевіряють правила торгів і вибірку, а не
/// доставку. Записуємо лише факт виклику — цього досить, щоб переконатися,
/// що сповіщення взагалі спробували надіслати.
/// </summary>
internal sealed class StubBotNotifier : IBotNotifier
{
    public List<long> NewMatchesFor { get; } = [];

    public List<long> OutbidFor { get; } = [];

    /// <summary>Скільки чатів «отримає» повідомлення. За замовчуванням — жодного.</summary>
    public int Chats { get; init; }

    public Task<int> NotifyNewMatchesAsync(
        long userId,
        string searchName,
        IReadOnlyList<ListingSummary> found,
        int total,
        CancellationToken cancellationToken = default)
    {
        NewMatchesFor.Add(userId);

        return Task.FromResult(Chats);
    }

    public Task NotifyOutbidAsync(
        long userId,
        string listingTitle,
        string price,
        long listingId,
        CancellationToken cancellationToken = default)
    {
        OutbidFor.Add(userId);

        return Task.CompletedTask;
    }
}
