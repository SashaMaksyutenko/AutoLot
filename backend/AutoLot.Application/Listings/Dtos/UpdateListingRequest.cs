using AutoLot.Domain.Enums;

namespace AutoLot.Application.Listings.Dtos;

/// <summary>
/// Редагування. Тип оголошення після створення не міняється: перевести
/// класифайд в аукціон означало б інші правила ціни й строків.
/// </summary>
public sealed record UpdateListingRequest
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public long CityId { get; init; }

    public long? CityDistrictId { get; init; }

    public decimal Price { get; init; }

    public Currency Currency { get; init; }

    /// <summary>Лише для лота з торгами: нижня межа, за якою продавець згоден віддати авто.</summary>
    public decimal? ReservePrice { get; init; }

    public bool IsNegotiable { get; init; }

    public bool AcceptsTrade { get; init; }

    public bool IsUrgent { get; init; }

    public CarSpecification Car { get; init; } = new();
}
