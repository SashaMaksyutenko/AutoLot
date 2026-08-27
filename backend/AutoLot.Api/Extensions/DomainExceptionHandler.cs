using AutoLot.Application.Auctions;
using AutoLot.Application.Listings;
using AutoLot.Application.Users;
using AutoLot.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Extensions;

/// <summary>
/// Перетворює очікувані винятки прикладного шару на коди відповіді.
///
/// Один обробник на весь застосунок замість try/catch у кожній дії: правило
/// «порушення домену — це 409» описане тут одного разу й діє скрізь, а нові
/// контролери отримують його безкоштовно.
/// </summary>
internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var (statusCode, title) = exception switch
        {
            ListingNotFoundException => (StatusCodes.Status404NotFound, "Оголошення не знайдено"),
            QuestionNotFoundException => (StatusCodes.Status404NotFound, "Питання не знайдено"),
            ListingAccessException => (StatusCodes.Status403Forbidden, "Немає доступу"),
            ListingDataException => (StatusCodes.Status400BadRequest, "Некоректні дані"),

            AuctionNotFoundException => (StatusCodes.Status404NotFound, "Торгів не знайдено"),

            // Саме 403, а не 409: продавцю не «зараз не можна», а не можна
            // взагалі — скільки б він не чекав, на власний лот не поставить.
            BiddingNotAllowedException => (StatusCodes.Status403Forbidden, "Ставити не можна"),
            InvalidLocationException => (StatusCodes.Status400BadRequest, "Некоректне місцезнаходження"),

            // Порушення правила домену — саме конфлікт: запит коректний, але
            // сутність зараз у стані, який цієї дії не допускає.
            DomainRuleException => (StatusCodes.Status409Conflict, "Дію зараз виконати не можна"),

            // Решту не чіпаємо: несподівані винятки має обробити стандартний
            // механізм, який не покаже клієнту внутрішніх подробиць.
            _ => (0, string.Empty),
        };

        if (statusCode == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = statusCode;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = exception.Message,
            },
        });
    }
}
