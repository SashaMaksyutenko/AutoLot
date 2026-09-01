using AutoLot.Application.Cars.Dtos;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Listings;
using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Скарги на опубліковані оголошення.
///
/// Рішення модератора пишемо в лог тими самими словами, що й рішення
/// модерації: за SPEC §8 дії з чужим оголошенням підлягають аудиту, і поки
/// повноцінного аудит-логу немає, слід має лишатися хоча б там.
/// </summary>
internal sealed partial class ListingReportService(
    AutoLotDbContext dbContext,
    IDateTimeProvider clock,
    ICurrentLanguage language,
    ListingAccess access,
    ILogger<ListingReportService> logger) : IListingReportService
{
    public async Task<IReadOnlyList<LookupItem>> GetReasonsAsync(
        CancellationToken cancellationToken = default)
    {
        return await EnumTranslationLookup.GetAsync(
            dbContext,
            nameof(ListingReportReason),
            language.Code,
            cancellationToken);
    }

    public async Task<ReportReceipt> SubmitAsync(
        long listingId,
        long reporterId,
        SubmitReportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var listing = await dbContext.Listings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == listingId, cancellationToken)
            ?? throw new ListingNotFoundException(listingId);

        // Скаржитися можна лише на те, що видно людям. Чернетка чи вже
        // відхилене оголошення нікому не шкодять, а модератор витратив би
        // на них час.
        if (!listing.IsPublic)
        {
            throw new ListingNotFoundException(listingId);
        }

        // На власний лот скаржитися нема сенсу — як і менеджерові салону
        // на лот свого салону. Правило те саме, що й для решти дій з
        // оголошенням, тож живе в одному місці.
        if (await access.CanManageAsync(listing, reporterId, cancellationToken))
        {
            throw new ReportNotAllowedException("Це ваше оголошення.");
        }

        var existing = await dbContext.ListingReports
            .AsNoTracking()
            .FirstOrDefaultAsync(
                report => report.ListingId == listingId
                    && report.ReporterId == reporterId
                    && report.Status == ListingReportStatus.Pending,
                cancellationToken);

        // Друга скарга тієї самої людини на те саме оголошення нічого не
        // додає, лише подвоює чергу. Унікальним індексом це не виразити:
        // після розгляду поскаржитися знову можна — оголошення могло
        // змінитися.
        if (existing is not null)
        {
            return new ReportReceipt(
                existing.Id,
                listingId,
                existing.Reason,
                existing.CreatedAt,
                IsNew: false);
        }

        var comment = request.Comment?.Trim();

        var report = new ListingReport
        {
            ListingId = listingId,
            ReporterId = reporterId,
            Reason = request.Reason,
            Comment = string.IsNullOrEmpty(comment) ? null : comment,
            CreatedAt = clock.UtcNow,
        };

        dbContext.ListingReports.Add(report);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogSubmitted(logger, report.Id, listingId, report.Reason);

        return new ReportReceipt(report.Id, listingId, report.Reason, report.CreatedAt, IsNew: true);
    }

    public async Task<IReadOnlyList<ReportSummary>> GetQueueAsync(
        CancellationToken cancellationToken = default)
    {
        var code = language.Code;

        var rows = await dbContext.ListingReports
            .AsNoTracking()
            .Where(report => report.Status == ListingReportStatus.Pending)
            // Найдавніші першими: хто поскаржився раніше, той раніше й
            // дочекається рішення.
            .OrderBy(report => report.CreatedAt)
            .ThenBy(report => report.Id)
            .Select(report => new
            {
                report.Id,
                report.ListingId,
                report.Listing.Title,
                report.Listing.Price,
                Photo = report.Listing.Car.Photos
                    .Where(photo => photo.IsPrimary)
                    .Select(photo => photo.Path)
                    .FirstOrDefault(),
                report.Reason,
                report.Comment,
                ReporterName = report.Reporter.DisplayName,
                report.CreatedAt,

                // Скільки ще скарг на це саме оголошення в черзі. Рахуємо
                // тим самим запитом: окремий похід у базу на кожен рядок
                // черги — класична помилка «N+1».
                OtherPending = dbContext.ListingReports.Count(other =>
                    other.ListingId == report.ListingId
                    && other.Status == ListingReportStatus.Pending
                    && other.Id != report.Id),
            })
            .ToListAsync(cancellationToken);

        var reasons = await GetReasonsAsync(cancellationToken);

        return
        [
            .. rows.Select(row => new ReportSummary(
                row.Id,
                row.ListingId,
                row.Title,
                row.Photo,
                row.Price,
                row.Reason,
                NameOf(reasons, row.Reason),
                row.Comment,
                row.ReporterName,
                row.CreatedAt,
                row.OtherPending)),
        ];
    }

    public async Task ResolveAsync(
        long reportId,
        long moderatorId,
        ResolveReportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var report = await dbContext.ListingReports
            .Include(item => item.Listing)
            .FirstOrDefaultAsync(item => item.Id == reportId, cancellationToken)
            ?? throw new ReportNotFoundException(reportId);

        report.Resolve(request.Accepted, moderatorId, clock.UtcNow, request.Note);

        if (request.Accepted)
        {
            var reasons = await GetReasonsAsync(cancellationToken);

            TakeDown(report, NameOf(reasons, report.Reason));

            await CloseOthersAsync(report, moderatorId, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        LogResolved(logger, reportId, report.ListingId, moderatorId, request.Accepted);
    }

    /// <summary>
    /// Знімає оголошення з публікації. Якщо його вже там немає — автор сам
    /// заархівував, поки скарга чекала, — робити нічого: рішення записане,
    /// а оголошення й так поза очима.
    /// </summary>
    private static void TakeDown(ListingReport report, string reasonName)
    {
        if (!report.Listing.IsPublic)
        {
            return;
        }

        // Причина, яку побачить автор: назва словами, без імені скаржника й
        // без його коментаря. Коментар писали модератору, і в ньому може
        // бути що завгодно.
        report.Listing.TakeDown($"Знято з публікації за скаргою: {reasonName}.");
    }

    /// <summary>
    /// Закриває решту скарг на те саме оголошення.
    /// </summary>
    /// <remarks>
    /// Інакше модератор, знявши лот за першою скаргою, отримав би ще чотири
    /// про вже зняте — і мусив би розглядати зроблене. Спрацьовує лише при
    /// схваленні: якщо скаргу відхилено, інші могли бути про інше й мають
    /// право на власний розгляд.
    /// </remarks>
    private async Task CloseOthersAsync(
        ListingReport report,
        long moderatorId,
        CancellationToken cancellationToken)
    {
        var others = await dbContext.ListingReports
            .Where(other => other.ListingId == report.ListingId
                && other.Id != report.Id
                && other.Status == ListingReportStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var other in others)
        {
            other.Resolve(
                accepted: true,
                moderatorId,
                clock.UtcNow,
                $"Закрито разом зі скаргою №{report.Id}.");
        }
    }

    /// <summary>Назва причини словами; якщо перекладу не знайшлося — саме значення.</summary>
    private static string NameOf(IReadOnlyList<LookupItem> reasons, ListingReportReason reason)
    {
        var value = reason.ToString();

        return reasons.FirstOrDefault(item => item.Value == value)?.Name ?? value;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Скарга {ReportId} на оголошення {ListingId}, причина {Reason}")]
    private static partial void LogSubmitted(
        ILogger logger,
        long reportId,
        long listingId,
        ListingReportReason reason);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Скаргу {ReportId} на оголошення {ListingId} розглянув модератор {ModeratorId}; слушна: {Accepted}")]
    private static partial void LogResolved(
        ILogger logger,
        long reportId,
        long listingId,
        long moderatorId,
        bool accepted);
}
