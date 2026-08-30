using AutoLot.Application.Auth.Dtos;
using AutoLot.Application.Users.Dtos;

namespace AutoLot.Application.Users;

public interface IUserProfileService
{
    /// <summary>
    /// Зберігає місто й район міста користувача. Повертає <c>null</c>, якщо
    /// такого користувача немає, і кидає <see cref="InvalidLocationException"/>,
    /// якщо надіслані ідентифікатори не складаються в реальну адресу.
    /// </summary>
    /// <summary>
    /// Змінює ім'я й телефон. Пошта сюди не входить: її зміна — окремий
    /// сценарій із підтвердженням нової скриньки, інакше нею можна було б
    /// перехопити чужий акаунт через відновлення пароля.
    /// </summary>
    Task<UserProfile?> UpdateAsync(
        long userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<UserProfile?> UpdateLocationAsync(
        long userId,
        UpdateLocationRequest request,
        CancellationToken cancellationToken = default);
}
