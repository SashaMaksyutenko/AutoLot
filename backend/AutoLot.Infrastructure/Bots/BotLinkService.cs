using System.Security.Cryptography;
using System.Globalization;
using AutoLot.Application.Bots;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Bots;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoLot.Infrastructure.Bots;

internal sealed partial class BotLinkService(
    AutoLotDbContext dbContext,
    IDateTimeProvider clock,
    ILogger<BotLinkService> logger) : IBotLinkService
{
    public async Task<BotLinkCodeIssued> IssueCodeAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        // Старі невикористані коди цієї людини гасимо: якщо на екрані новий,
        // попередній не має лишатися робочим. Інакше код, підглянутий через
        // плече годину тому, працював би й далі.
        var previous = await dbContext.BotLinkCodes
            .Where(item => item.UserId == userId && item.UsedAt == null && item.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var code in previous)
        {
            code.ExpiresAt = now;
        }

        var issued = new BotLinkCode
        {
            UserId = userId,
            Code = NewCode(),
            ExpiresAt = now.Add(BotLinkCode.Lifetime),
        };

        dbContext.BotLinkCodes.Add(issued);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new BotLinkCodeIssued(issued.Code, issued.ExpiresAt);
    }

    public async Task<BotLinkOutcome> RedeemAsync(
        BotProvider provider,
        string chatId,
        string? chatName,
        string code,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var trimmed = code?.Trim() ?? string.Empty;

        var found = await dbContext.BotLinkCodes
            .FirstOrDefaultAsync(item => item.Code == trimmed, cancellationToken);

        if (found is null || !found.IsUsable(now))
        {
            LogCodeRejected(logger, provider, chatId);

            return BotLinkOutcome.CodeRejected;
        }

        var existing = await dbContext.BotLinks.FirstOrDefaultAsync(
            link => link.Provider == provider && link.ChatId == chatId,
            cancellationToken);

        // Код витрачено в будь-якому разі: навіть якщо чат уже прив'язаний,
        // другий раз цим кодом скористатися не можна.
        found.UsedAt = now;

        if (existing is not null && existing.UserId == found.UserId)
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            return BotLinkOutcome.AlreadyLinked;
        }

        if (existing is not null)
        {
            // Чат передали іншому акаунту — стара прив'язка більше не діє.
            // Двох господарів в одного чату бути не може: бот не зрозуміє,
            // кому надсилати сповіщення.
            dbContext.BotLinks.Remove(existing);
        }

        dbContext.BotLinks.Add(new BotLink
        {
            UserId = found.UserId,
            Provider = provider,
            ChatId = chatId,
            ChatName = chatName,
            LinkedAt = now,
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        LogLinked(logger, found.UserId, provider);

        return BotLinkOutcome.Linked;
    }

    public async Task<long?> FindUserAsync(
        BotProvider provider,
        string chatId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.BotLinks
            .AsNoTracking()
            .Where(link => link.Provider == provider && link.ChatId == chatId)
            .Select(link => (long?)link.UserId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> UnlinkAsync(
        BotProvider provider,
        string chatId,
        CancellationToken cancellationToken = default)
    {
        var link = await dbContext.BotLinks.FirstOrDefaultAsync(
            item => item.Provider == provider && item.ChatId == chatId,
            cancellationToken);

        if (link is null)
        {
            return false;
        }

        dbContext.BotLinks.Remove(link);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogUnlinked(logger, link.UserId, provider);

        return true;
    }

    public async Task<IReadOnlyList<BotRecipient>> GetRecipientsAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.BotLinks
            .AsNoTracking()
            .Where(link => link.UserId == userId)
            .Select(link => new BotRecipient(link.UserId, link.Provider, link.ChatId))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Шість цифр із криптографічного генератора, а не з Random.
    ///
    /// Звичайний Random передбачуваний: знаючи кілька виданих кодів, наступні
    /// можна обчислити. Тут це означало б чужу прив'язку, тож генератор
    /// потрібен саме той, що для таких випадків і зроблений.
    /// </summary>
    private static string NewCode()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);

        return value.ToString("D" + BotLinkCode.Length, CultureInfo.InvariantCulture);
    }

    // ── Журнал ───────────────────────────────────────────────────────
    //
    // Прив'язка дає месенджеру право отримувати сповіщення про чужі ставки
    // й пошуки — тобто це зміна прав, а їх ми пишемо (SPEC §8). Сам код у
    // лог не потрапляє: поки він живий, це діючий ключ.

    [LoggerMessage(
        EventId = 240,
        Level = LogLevel.Information,
        Message = "Користувач {UserId} прив'язав чат {Provider}")]
    private static partial void LogLinked(ILogger logger, long userId, BotProvider provider);

    [LoggerMessage(
        EventId = 241,
        Level = LogLevel.Information,
        Message = "Користувач {UserId} відв'язав чат {Provider}")]
    private static partial void LogUnlinked(ILogger logger, long userId, BotProvider provider);

    [LoggerMessage(
        EventId = 242,
        Level = LogLevel.Warning,
        Message = "Відхилено код прив'язки для {Provider}, чат {ChatId}")]
    private static partial void LogCodeRejected(ILogger logger, BotProvider provider, string chatId);
}
