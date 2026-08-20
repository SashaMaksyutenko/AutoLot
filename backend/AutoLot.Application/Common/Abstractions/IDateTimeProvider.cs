namespace AutoLot.Application.Common.Abstractions;

/// <summary>
/// Єдине джерело часу для застосунку. Аукціон живе за годинником сервера
/// (див. SPEC §5), а тести мають могти цей годинник підмінити.
/// </summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
