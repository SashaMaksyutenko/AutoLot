using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoLot.Api.Extensions;

public static class RateLimitingSetup
{
    /// <summary>Вхід і реєстрація — найпривабливіші цілі для перебору (SPEC §8).</summary>
    public const string AuthPolicy = "auth";

    /// <summary>Скільки спроб за хвилину дозволено з однієї адреси.</summary>
    private const int DefaultPermitLimit = 10;

    public static IServiceCollection AddAutoLotRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        /*
            Межа береться з конфігурації, а не зашита в код.

            Причина не в гнучкості заради гнучкості: інтеграційні тести ходять
            на /api/auth десятки разів поспіль, і з межею в десять запитів
            половина з них отримувала б 429 замість того, що перевіряє. Межу
            такого роду взагалі природно тримати в налаштуваннях — під
            навантажувальний прогін вона теж інша.

            Значення за замовчуванням лишається тим самим, тож поведінка
            робочого сервера не змінюється.
        */
        var permitLimit = configuration.GetValue("RateLimiting:AuthPermitLimit", DefaultPermitLimit);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(AuthPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: PartitionKey(httpContext),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { error = "Забагато запитів. Спробуйте трохи пізніше." },
                    cancellationToken);
            };
        });

        return services;
    }

    /// <summary>
    /// Ділимо за IP. Автентифікований користувач тут неважливий: обмеження
    /// стосується саме спроб увійти, коли токена ще немає.
    /// </summary>
    private static string PartitionKey(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
