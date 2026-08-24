namespace AutoLot.Application.Listings.Dtos;

public sealed record ListingPhoto(
    long Id,
    string Path,
    string ThumbnailPath,
    int SortOrder,
    bool IsPrimary);
