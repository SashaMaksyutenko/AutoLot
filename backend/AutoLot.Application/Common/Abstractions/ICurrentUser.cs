namespace AutoLot.Application.Common.Abstractions;

/// <summary>
/// Хто виконує поточний запит. Сценарії не мають лазити в HttpContext, а права
/// ніколи не беруться з тіла запиту (SPEC §8).
/// </summary>
public interface ICurrentUser
{
    long? Id { get; }

    string? Email { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string role);
}
