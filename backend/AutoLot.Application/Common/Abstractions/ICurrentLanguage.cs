namespace AutoLot.Application.Common.Abstractions;

/// <summary>
/// Мова поточного запиту. Її визначає шар API із заголовка Accept-Language,
/// а сценарії просто беруть готовий код і не знають про HTTP нічого.
/// </summary>
public interface ICurrentLanguage
{
    /// <summary>Код із <see cref="Domain.Common.LanguageCodes"/>: "uk" або "en".</summary>
    string Code { get; }
}
