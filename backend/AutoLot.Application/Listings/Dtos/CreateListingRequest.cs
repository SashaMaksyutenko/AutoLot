using AutoLot.Domain.Enums;

namespace AutoLot.Application.Listings.Dtos;

/// <summary>
/// Те, що надсилає автор при створенні оголошення. Продавець і статус сюди
/// не входять навмисно: перший береться з токена, другий — завжди чернетка.
/// </summary>
public sealed record CreateListingRequest
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public long CityId { get; init; }

    public long? CityDistrictId { get; init; }

    public decimal Price { get; init; }

    public Currency Currency { get; init; }

    public ListingType Type { get; init; } = ListingType.FixedPrice;

    public bool IsNegotiable { get; init; }

    public bool AcceptsTrade { get; init; }

    public bool IsUrgent { get; init; }

    public CarSpecification Car { get; init; } = new();
}
