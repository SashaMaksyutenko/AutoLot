namespace AutoLot.Application.Listings;

/// <summary>Оголошення немає або воно недоступне тому, хто питає.</summary>
public sealed class ListingNotFoundException(long listingId)
    : Exception($"Оголошення {listingId} не знайдено.");

/// <summary>
/// У запиті вказані ідентифікатори, яким нічого не відповідає, або вони не
/// узгоджуються між собою — наприклад, модель належить іншій марці.
/// </summary>
public sealed class ListingDataException(string message) : Exception(message);
