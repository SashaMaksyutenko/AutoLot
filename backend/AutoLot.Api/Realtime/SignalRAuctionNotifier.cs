using AutoLot.Application.Auctions;
using AutoLot.Application.Auctions.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace AutoLot.Api.Realtime;

/// <summary>
/// Реалізація розсилки через SignalR. Живе в шарі Api, бо SignalR — це
/// подробиця вебсервера: сценаріям достатньо інтерфейсу
/// <see cref="IAuctionNotifier"/>, і замінити спосіб доставки можна, не
/// торкнувшись жодного правила торгів.
/// </summary>
internal sealed class SignalRAuctionNotifier(IHubContext<AuctionHub> hub) : IAuctionNotifier
{
    public Task BidPlacedAsync(AuctionUpdate update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        // "bidPlaced" — назва, на яку підписується браузер. Вона мусить
        // збігатися з тією, що у фронтенді (src/api/auctionHub.ts).
        return hub.Clients
            .Group(AuctionHub.GroupFor(update.ListingId))
            .SendAsync("bidPlaced", update, cancellationToken);
    }

    public Task AuctionEndedAsync(AuctionOutcome outcome, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return hub.Clients
            .Group(AuctionHub.GroupFor(outcome.ListingId))
            .SendAsync("auctionEnded", outcome, cancellationToken);
    }
}
