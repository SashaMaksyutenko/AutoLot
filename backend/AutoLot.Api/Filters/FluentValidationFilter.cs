using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Common.Localization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AutoLot.Api.Filters;

/// <summary>
/// Проганяє кожен аргумент дії через зареєстрований для нього валідатор
/// FluentValidation. Так вимога «валідація на всіх вхідних DTO» (SPEC §8)
/// виконується автоматично, а не завдяки пам'яті автора контролера.
/// </summary>
internal sealed class FluentValidationFilter(
    IServiceProvider services,
    ICurrentLanguage language) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());

            if (services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            foreach (var failure in result.Errors)
            {
                // Валідатор повернув КОД правила; у слова його переводимо тут,
                // де вже відома мова запиту. Прикладний рівень про мову не знає
                // й знати не повинен.
                context.ModelState.AddModelError(
                    failure.PropertyName,
                    MessageCatalog.Translate(failure.ErrorMessage, language.Code));
            }
        }

        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
            return;
        }

        await next();
    }
}
