using AutoLot.Application.Listings.Dtos;

namespace AutoLot.Application.Listings;

/// <summary>
/// Фото оголошення. Кожен метод перевіряє, що діє власник — фото такий самий
/// ресурс, як і саме оголошення.
/// </summary>
public interface IListingPhotoService
{
    Task<IReadOnlyList<ListingPhoto>> GetAsync(
        long listingId,
        long actorId,
        CancellationToken cancellationToken = default);

    Task<ListingPhoto> AddAsync(
        long listingId,
        long actorId,
        PhotoUpload upload,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        long listingId,
        long actorId,
        long photoId,
        CancellationToken cancellationToken = default);

    /// <summary>Порядок задається повним списком: клієнт надсилає всі фото в потрібній послідовності.</summary>
    Task ReorderAsync(
        long listingId,
        long actorId,
        IReadOnlyList<long> photoIdsInOrder,
        CancellationToken cancellationToken = default);

    Task SetPrimaryAsync(
        long listingId,
        long actorId,
        long photoId,
        CancellationToken cancellationToken = default);
}
