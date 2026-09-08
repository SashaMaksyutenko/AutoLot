using AutoLot.Api.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoLot.Tests.Infrastructure;

/// <summary>
/// Наскрізний ідентифікатор запиту.
///
/// Найважливіше тут — поводження з ЧУЖИМ значенням. Клієнт може надіслати
/// що завгодно, а воно потрапляє просто в лог; переведення рядка в ньому
/// дало б зловмиснику змогу домалювати підроблений запис і сховати серед
/// нього свої дії.
/// </summary>
public class RequestIdMiddlewareTests
{
    [Fact]
    public async Task Without_a_header_the_server_makes_up_its_own()
    {
        var context = NewContext();
        context.TraceIdentifier = "0HN-SERVER-SIDE";

        await Run(context);

        Assert.Equal("0HN-SERVER-SIDE", Returned(context));
    }

    [Fact]
    public async Task A_sane_client_identifier_is_kept()
    {
        var context = NewContext();
        context.Request.Headers[RequestIdMiddleware.HeaderName] = "abc-123_XY:7";

        await Run(context);

        // Своє значення клієнта — не примха: воно дає змогу зшити слід
        // через кілька служб, а не лише всередині цієї.
        Assert.Equal("abc-123_XY:7", Returned(context));
    }

    [Theory]
    [InlineData("щось\nfake: підроблений запис")]
    [InlineData("рядок\rз поверненням каретки")]
    [InlineData("пробіл всередині")]
    [InlineData("лапки\"й<кутики>")]
    public async Task A_forged_identifier_is_thrown_away(string forged)
    {
        var context = NewContext();
        context.TraceIdentifier = "власний";
        context.Request.Headers[RequestIdMiddleware.HeaderName] = forged;

        await Run(context);

        // Підозріле не «чистимо», а відкидаємо цілком: обрізаний до
        // безпечного вигляду рядок усе одно лишився б підробкою.
        Assert.Equal("власний", Returned(context));
    }

    [Fact]
    public async Task An_over_long_identifier_is_thrown_away()
    {
        var context = NewContext();
        context.TraceIdentifier = "власний";
        context.Request.Headers[RequestIdMiddleware.HeaderName] = new string('a', 200);

        await Run(context);

        // Рядок на кілобайт у кожному записі роздув би логи в стократ.
        Assert.Equal("власний", Returned(context));
    }

    [Fact]
    public async Task An_empty_header_is_the_same_as_none()
    {
        var context = NewContext();
        context.TraceIdentifier = "власний";
        context.Request.Headers[RequestIdMiddleware.HeaderName] = "   ";

        await Run(context);

        Assert.Equal("власний", Returned(context));
    }

    [Fact]
    public async Task The_identifier_is_set_before_the_response_starts()
    {
        var context = NewContext();
        context.TraceIdentifier = "власний";

        string? seenInsideHandler = null;

        await Run(context, () =>
        {
            // Наступний обробник уже має бачити заголовок: після того, як
            // відповідь пішла в мережу, дописати його вже неможливо, а
            // потрібен він саме на помилкових відповідях.
            seenInsideHandler = context.Response.Headers[RequestIdMiddleware.HeaderName];

            return Task.CompletedTask;
        });

        Assert.Equal("власний", seenInsideHandler);
    }

    private static DefaultHttpContext NewContext() => new();

    private static string? Returned(HttpContext context) =>
        context.Response.Headers[RequestIdMiddleware.HeaderName];

    private static Task Run(HttpContext context, Func<Task>? next = null)
    {
        var middleware = new RequestIdMiddleware(
            _ => next is null ? Task.CompletedTask : next(),
            NullLogger<RequestIdMiddleware>.Instance);

        return middleware.InvokeAsync(context);
    }
}
