using AutoLot.Application.Auth;
using AutoLot.Application.Auth.Dtos;
using AutoLot.Application.Geo;
using AutoLot.Application.Users;
using AutoLot.Application.Users.Dtos;
using AutoLot.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AutoLot.Domain.Common;

namespace AutoLot.Infrastructure.Identity;

internal sealed partial class UserProfileService(
    UserManager<User> userManager,
    IGeoCatalog geoCatalog,
    IAuthService authService,
    ILogger<UserProfileService> logger) : IUserProfileService
{
    public async Task<UserProfile?> UpdateAsync(
        long userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await userManager.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        user.DisplayName = request.DisplayName.Trim();

        var phone = request.PhoneNumber?.Trim();
        var newPhone = string.IsNullOrEmpty(phone) ? null : phone;

        // Змінили номер — підтвердження старого до нового не стосується.
        // Поки підтвердження телефону не реалізоване, прапорець просто
        // скидається; коли з'явиться SMS, ця гілка вже буде на місці.
        if (user.PhoneNumber != newPhone)
        {
            user.PhoneNumber = newPhone;
            user.PhoneNumberConfirmed = false;
        }

        await userManager.UpdateAsync(user);

        LogProfileChanged(logger, userId, user.PhoneNumber is not null);

        return await authService.GetProfileAsync(userId, cancellationToken);
    }

    public async Task<UserProfile?> UpdateLocationAsync(
        long userId,
        UpdateLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await userManager.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (request.CityId is { } cityId)
        {
            // Ідентифікатори прийшли від клієнта, тож перевіряємо не лише
            // існування міста, а й те, що район належить саме йому.
            var exists = await geoCatalog.LocationExistsAsync(
                cityId,
                request.CityDistrictId,
                cancellationToken);

            if (!exists)
            {
                throw new InvalidLocationException(
                    MessageCodes.PlaceCityOrDistrictInvalid);
            }

            user.CityId = cityId;
            user.CityDistrictId = request.CityDistrictId;
        }
        else
        {
            user.CityId = null;
            user.CityDistrictId = null;
        }

        await userManager.UpdateAsync(user);

        return await authService.GetProfileAsync(userId, cancellationToken);
    }

    /// <remarks>
    /// Сам номер у лог не пишемо — це особисті дані, і в базі він уже є.
    /// Записуємо лише ФАКТ зміни: він відповідає на питання «чому листи
    /// раптом пішли не туди», а для цього номер не потрібен.
    /// </remarks>
    [LoggerMessage(
        EventId = 230,
        Level = LogLevel.Information,
        Message = "Профіль користувача {UserId} змінено; телефон вказано: {HasPhone}")]
    private static partial void LogProfileChanged(ILogger logger, long userId, bool hasPhone);
}
