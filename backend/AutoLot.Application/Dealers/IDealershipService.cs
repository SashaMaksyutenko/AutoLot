using AutoLot.Application.Dealers.Dtos;
using AutoLot.Domain.Dealers;

namespace AutoLot.Application.Dealers;

/// <summary>
/// Автосалони та їхній персонал.
///
/// Ролі всередині салону (власник / менеджер) не плутати з ролями майданчика
/// (`User` / `Moderator` / `Admin`): перші діють у межах одного салону, другі —
/// на всьому сайті.
/// </summary>
public interface IDealershipService
{
    /// <summary>Публічна картка салону. null, якщо такого немає.</summary>
    Task<DealershipDetails?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Каталог салонів. Перевірені йдуть першими — саме заради цього бейдж
    /// і потрібен; далі за кількістю активних оголошень, бо порожня вітрина
    /// покупцеві ні до чого.
    /// </summary>
    Task<IReadOnlyList<DealershipCard>> SearchAsync(
        string? text,
        long? cityId,
        bool verifiedOnly,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Створює салон. Той, хто створив, автоматично стає власником — інакше
    /// новий салон лишився б без нікого, хто може додати персонал.
    /// </summary>
    Task<DealershipDetails> CreateAsync(
        long founderId,
        CreateDealershipRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Салони, де людина працює, з її роллю в кожному.</summary>
    Task<IReadOnlyList<DealershipMembership>> GetMembershipsAsync(
        long userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StaffMember>> GetStaffAsync(
        long dealershipId,
        long actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Додає людину до салону. Дозволено лише власникові.</summary>
    Task AddStaffAsync(
        long dealershipId,
        long actorId,
        string email,
        DealershipRole role,
        CancellationToken cancellationToken = default);

    /// <summary>Прибирає людину із салону. Дозволено лише власникові.</summary>
    Task RemoveStaffAsync(
        long dealershipId,
        long actorId,
        long userId,
        CancellationToken cancellationToken = default);

    /// <summary>Ставить або знімає бейдж перевіреного. Дозволено лише модератору.</summary>
    Task SetVerificationAsync(
        long dealershipId,
        long moderatorId,
        bool isVerified,
        CancellationToken cancellationToken = default);
}

/// <summary>Салону немає.</summary>
public sealed class DealershipNotFoundException(string what)
    : Exception($"Салон {what} не знайдено.");

/// <summary>Дію намагається виконати не той, кому вона належить.</summary>
public sealed class DealershipAccessException(string message) : Exception(message);
