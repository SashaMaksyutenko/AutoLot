namespace AutoLot.Application.Common;

/// <summary>
/// Сторінка результатів разом із загальною кількістю. Кількість потрібна, щоб
/// намалювати пагінацію: без неї клієнт не знає, скільки сторінок попереду.
/// </summary>
public sealed record PagedResult<TItem>(
    IReadOnlyList<TItem> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}
