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
    Task<UserProfile?> UpdateLocationAsync(
        long userId,
        UpdateLocationRequest request,
        CancellationToken cancellationToken = default);
}
