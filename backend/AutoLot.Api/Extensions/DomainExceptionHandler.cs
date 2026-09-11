using AutoLot.Application.Admin;
using AutoLot.Application.Auctions;
using AutoLot.Application.Billing;
using AutoLot.Application.Chat;
using AutoLot.Application.Dealers;
using AutoLot.Application.Listings;
using AutoLot.Application.Search;
using AutoLot.Application.Users;
using AutoLot.Application.Common.Localization;
using AutoLot.Domain.Billing;
using AutoLot.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Localization;
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

        /*
          Мову читаємо з ОЗНАКИ ЗАПИТУ, а не зі звичного ICurrentLanguage.

          Причина тонка. ICurrentLanguage дивиться на CultureInfo поточного
          потоку, а її ставить UseRequestLocalization — і воно ж повертає
          попередню культуру у своєму finally. Обробник винятків стоїть у
          конвеєрі ВИЩЕ, тож виняток дістається сюди вже після того відкоту:
          мова знову типова, і англієць отримав би українську відповідь.

          Ознака ж лежить на самому HttpContext і нікуди не зникає, тож не
          залежить ні від потоку, ні від порядку middleware — а порядок
          хтось цілком може змінити, не здогадуючись про цей зв'язок.
        */
        var requested = httpContext.Features.Get<IRequestCultureFeature>()
            ?.RequestCulture.UICulture.TwoLetterISOLanguageName;

        var language = LanguageCodes.Normalize(requested);

        var (statusCode, titleCode) = exception switch
        {
            ListingNotFoundException => (StatusCodes.Status404NotFound, MessageCodes.TitleListingNotFound),
            QuestionNotFoundException => (StatusCodes.Status404NotFound, MessageCodes.TitleQuestionNotFound),
            ReportNotFoundException => (StatusCodes.Status404NotFound, MessageCodes.TitleReportNotFound),
            ReportNotAllowedException => (StatusCodes.Status403Forbidden, MessageCodes.TitleReportNotAllowed),
            ReviewNotAllowedException => (StatusCodes.Status403Forbidden, MessageCodes.TitleReviewNotAllowed),
            SavedSearchNotFoundException => (StatusCodes.Status404NotFound, MessageCodes.TitleSavedSearchNotFound),
            PlanNotFoundException => (StatusCodes.Status404NotFound, MessageCodes.TitlePlanNotFound),
            SubscriptionNotAllowedException => (StatusCodes.Status403Forbidden, MessageCodes.TitleSubscriptionNotAllowed),

            // 409, а не 400: запит правильний, просто коштів зараз бракує.
            // Поповнити баланс — і той самий запит спрацює.
            InsufficientFundsException => (StatusCodes.Status409Conflict, MessageCodes.TitleInsufficientFunds),
            ListingAccessException => (StatusCodes.Status403Forbidden, MessageCodes.TitleNoAccess),
            DealershipAccessException => (StatusCodes.Status403Forbidden, MessageCodes.TitleNoAccess),
            DealershipNotFoundException => (StatusCodes.Status404NotFound, MessageCodes.TitleDealershipNotFound),
            UserNotFoundException => (StatusCodes.Status404NotFound, MessageCodes.TitleUserNotFound),
            AdminActionException => (StatusCodes.Status400BadRequest, MessageCodes.TitleAdminActionForbidden),
            ConversationNotFoundException => (StatusCodes.Status404NotFound, MessageCodes.TitleConversationNotFound),
            ChatNotAllowedException => (StatusCodes.Status403Forbidden, MessageCodes.TitleChatNotAllowed),
            ListingDataException => (StatusCodes.Status400BadRequest, MessageCodes.TitleInvalidData),

            AuctionNotFoundException => (StatusCodes.Status404NotFound, MessageCodes.TitleAuctionNotFound),

            // Саме 403, а не 409: продавцю не «зараз не можна», а не можна
            // взагалі — скільки б він не чекав, на власний лот не поставить.
            BiddingNotAllowedException => (StatusCodes.Status403Forbidden, MessageCodes.TitleBiddingNotAllowed),
            InvalidLocationException => (StatusCodes.Status400BadRequest, MessageCodes.TitleInvalidLocation),

            // Порушення правила домену — саме конфлікт: запит коректний, але
            // сутність зараз у стані, який цієї дії не допускає.
            DomainRuleException => (StatusCodes.Status409Conflict, MessageCodes.TitleRuleViolated),

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

                // І заголовок, і подробиця приходять сюди КОДАМИ: перший
                // назвав обробник, другий — той, хто кинув виняток. Слова
                // добираються тут, бо це остання точка, де ще відома мова
                // запиту.
                //
                // Значення для підстановки виняток везе у своєму Data — так
                // «до {limit} фото» стає «до 20 фото», не знаючи ні мови, ні
                // самого тексту.
                Title = MessageCatalog.Translate(titleCode, language),
                Detail = MessageCatalog.Translate(
                    exception.Message,
                    language,
                    exception.ValuesOf()),
            },
        });
    }
}
