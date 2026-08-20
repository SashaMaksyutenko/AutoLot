using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AutoLot.Api.Extensions;

internal static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        // Текст помилки може містити рядок підключення — назовні його не віддаємо.
        var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
        var includeErrors = environment.IsDevelopment();

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1),
                description = entry.Value.Description,
                error = includeErrors ? entry.Value.Exception?.Message : null,
            }),
        };

        return context.Response.WriteAsJsonAsync(payload, SerializerOptions);
    }
}
