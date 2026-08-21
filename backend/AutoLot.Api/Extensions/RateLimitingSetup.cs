using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoLot.Api.Extensions;

public static class RateLimitingSetup
{
    /// <summary>Вхід і реєстрація — найпривабливіші цілі для перебору (SPEC §8).</summary>
    public const string AuthPolicy = "auth";

    public static IServiceCollection AddAutoLotRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(AuthPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: PartitionKey(httpContext),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
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
