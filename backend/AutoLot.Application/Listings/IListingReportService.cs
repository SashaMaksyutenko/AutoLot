using AutoLot.Application.Cars.Dtos;
using AutoLot.Application.Listings.Dtos;

namespace AutoLot.Application.Listings;

/// <summary>
/// Скарги на оголошення.
///
/// Межа з модерацією: <c>IModerationService</c> розглядає те, що автор сам
/// подав на розгляд, а тут — те, що вже опубліковано і на що поскаржилися
/// відвідувачі. Черги дві, бо це різна робота: там «чи можна пускати», тут
/// «чи треба знімати».
/// </summary>
public interface IListingReportService
{
    /// <summary>Причини скарги з назвами мовою відвідувача — для випадаючого списку.</summary>
    Task<IReadOnlyList<LookupItem>> GetReasonsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Приймає скаргу. Повторна скарга тієї самої людини на те саме
    /// оголошення нової не створює — повертається вже наявна.
    /// </summary>
    Task<ReportReceipt> SubmitAsync(
        long listingId,
        long reporterId,
        SubmitReportRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Нерозглянуті скарги, найдавніші першими.</summary>
    Task<IReadOnlyList<ReportSummary>> GetQueueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Рішення модератора. Слушна скарга знімає оголошення з публікації —
    /// і закриває решту скарг на нього, бо вони вже про зроблене.
    /// </summary>
    Task ResolveAsync(
        long reportId,
        long moderatorId,
        ResolveReportRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Такої скарги немає.</summary>
public sealed class ReportNotFoundException(long reportId)
    : Exception($"Скаргу {reportId} не знайдено.");

/// <summary>Скаржитися в цьому випадку не можна — наприклад, на власний лот.</summary>
public sealed class ReportNotAllowedException(string message) : Exception(message);
