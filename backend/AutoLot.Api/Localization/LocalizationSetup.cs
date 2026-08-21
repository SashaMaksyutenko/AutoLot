using System.Globalization;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Common;
using Microsoft.AspNetCore.Localization;

namespace AutoLot.Api.Localization;

public static class LocalizationSetup
{
    public static IServiceCollection AddAutoLotLocalization(this IServiceCollection services)
    {
        var supported = LanguageCodes.Supported
            .Select(code => new CultureInfo(code))
            .ToList();

        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture(LanguageCodes.Default);
            options.SupportedCultures = supported;
            options.SupportedUICultures = supported;

            // Мову беремо лише із заголовка Accept-Language. Cookie та
            // ?culture= прибираємо, щоб джерело істини було одне.
            options.RequestCultureProviders =
                [new AcceptLanguageHeaderRequestCultureProvider()];
        });

        services.AddScoped<ICurrentLanguage, CurrentLanguage>();

        return services;
    }
}
