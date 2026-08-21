using System.Globalization;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Common;

namespace AutoLot.Api.Localization;

/// <summary>
/// Мову запиту вже розібрав вбудований у ASP.NET механізм локалізації: він
/// прочитав Accept-Language з усіма його вагами й обрав найкращу з дозволених.
/// Нам лишається взяти результат і звести до нашого дволітерного коду.
/// </summary>
internal sealed class CurrentLanguage : ICurrentLanguage
{
    public string Code => LanguageCodes.Normalize(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
}
