using AutoLot.Application.Auctions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoLot.Infrastructure.Auctions;

/// <summary>
/// Відновлює розклад закриттів після запуску застосунку.
///
/// Розклад Quartz живе в пам'яті процесу: зупинили сервер — усі заплановані
/// задачі зникли разом із ним. Без цього класу торги, що мали закритися вночі
/// під час перезапуску, лишалися б активними назавжди.
///
/// Робить дві речі: закриває все прострочене й наново замовляє задачі для
/// тих, що ще тривають.
///
/// BackgroundService, а не IDataSeeder: сідери виконуються до першого запиту й
/// послідовно, а тут не можна затримувати старт — активних лотів може бути
/// багато, і кожен потребує звернення до бази.
/// </summary>
internal sealed partial class AuctionScheduleRecovery(
    IServiceScopeFactory scopeFactory,
    ILogger<AuctionScheduleRecovery> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Власна область життя: цей клас — одинак, а сервіси навколо бази
            // живуть на час запиту. Брати їх напряму означало б тримати один
            // контекст бази відкритим на весь час роботи застосунку.
            await using var scope = scopeFactory.CreateAsyncScope();

            var closer = scope.ServiceProvider.GetRequiredService<IAuctionCloser>();
            var scheduler = scope.ServiceProvider.GetRequiredService<IAuctionScheduler>();

            var pending = await closer.CloseOverdueAndListPendingAsync(stoppingToken);

            foreach (var auction in pending)
            {
                await scheduler.ScheduleCloseAsync(auction.ListingId, auction.EndsAt, stoppingToken);
            }

            LogRestored(logger, pending.Count);
        }
        catch (OperationCanceledException)
        {
            // Застосунок зупиняють — це не збій.
        }
        catch (Exception error)
        {
            // База може бути недоступна на старті. Валити застосунок через це
            // не варто: він підніметься, про проблему чесно розкаже /health,
            // а розклад відновиться з наступним перезапуском.
            LogFailed(logger, error);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Розклад торгів відновлено: заплановано закриттів — {Count}.")]
    private static partial void LogRestored(ILogger logger, int count);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Не вдалося відновити розклад торгів.")]
    private static partial void LogFailed(ILogger logger, Exception error);
}
