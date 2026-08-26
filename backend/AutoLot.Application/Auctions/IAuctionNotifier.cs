using AutoLot.Application.Auctions.Dtos;

namespace AutoLot.Application.Auctions;

/// <summary>
/// Розсилає новини торгів усім, хто дивиться лот просто зараз.
///
/// Оголошено тут, а реалізовано в шарі Api: сама технологія розсилки
/// (SignalR) — це подробиця вебсервера, і сценаріям про неї знати не треба.
/// Завдяки цьому Infrastructure не тягне за собою ASP.NET Core, а напрямок
/// залежностей із SPEC §2 лишається цілим.
/// </summary>
public interface IAuctionNotifier
{
    Task BidPlacedAsync(AuctionUpdate update, CancellationToken cancellationToken = default);
}
