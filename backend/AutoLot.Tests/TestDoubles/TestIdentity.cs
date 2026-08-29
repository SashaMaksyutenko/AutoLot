using AutoLot.Domain.Identity;
using AutoLot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AutoLot.Tests.TestDoubles;

/// <summary>
/// Збирає <see cref="UserManager{TUser}"/> вручну для тестів.
///
/// У застосунку його складає контейнер із десятка залежностей; тут потрібне
/// лише те, чим користується адмінка: сховище користувачів і ролей. Решту
/// заповнюємо порожніми реалізаціями — жодна з них у цих тестах не працює.
/// </summary>
internal static class TestIdentity
{
    public static UserManager<User> CreateUserManager(AutoLotDbContext context)
    {
        var store = new UserStore<User, Role, AutoLotDbContext, long>(context);

        var options = Options.Create(new IdentityOptions());

        return new UserManager<User>(
            store,
            options,
            new PasswordHasher<User>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            services: null!,
            NullLogger<UserManager<User>>.Instance);
    }
}
