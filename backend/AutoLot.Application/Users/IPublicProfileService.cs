using AutoLot.Application.Users.Dtos;

namespace AutoLot.Application.Users;

/// <summary>
/// Профіль продавця очима стороннього.
///
/// Окремо від <see cref="IUserProfileService"/>, і не заради симетрії. Той
/// **змінює власний** профіль: йому потрібні Identity, зміна пароля, ролі.
/// Цьому не потрібне нічого, крім читання, — а надто права щось міняти.
/// Розділені інтерфейси означають, що випадково відкрити зміну профілю
/// назовні просто нічим.
/// </summary>
public interface IPublicProfileService
{
    /// <summary>Профіль продавця. <c>null</c>, якщо такої людини немає.</summary>
    Task<PublicProfile?> GetAsync(long userId, CancellationToken cancellationToken = default);
}
