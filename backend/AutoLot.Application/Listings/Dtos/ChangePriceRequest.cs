using AutoLot.Domain.Enums;

namespace AutoLot.Application.Listings.Dtos;

/// <summary>
/// Нова ціна для вже опублікованого оголошення.
///
/// Резерву тут немає навмисно: він стосується лише торгів, а в торгах ціну
/// змінювати не можна взагалі.
/// </summary>
public sealed record ChangePriceRequest(decimal Price, Currency Currency);
