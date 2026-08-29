using AutoLot.Application.Admin;
using AutoLot.Application.Admin.Dtos;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Identity;
using AutoLot.Infrastructure.Admin;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Tests.TestDoubles;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoLot.Tests.Admin;

/// <summary>
/// Керування людьми. Найважливіше тут — дві заборони, які рятують від
/// незворотної помилки: не заблокувати себе й не зняти з себе адміністратора.
///
/// Тести йдуть на СПРАВЖНЬОМУ PostgreSQL, бо пошук користувачів написаний
/// через ILIKE — синтаксис саме цієї бази. SQLite його не розуміє, і
/// підганяти робочий запит під обмеження тестового двійника було б
/// неправильно: у житті пошук має бути нечутливим до регістру, зокрема
/// в кирилиці.
/// </summary>
public class AdminServiceTests : IAsyncLifetime
{
    private const long AdminId = 1;

    private const long PersonId = 2;

    private const long OtherId = 3;

    private PostgresTestDatabase database = null!;

    private AutoLotDbContext context = null!;

    private UserManager<User> userManager = null!;

    public async Task InitializeAsync()
    {
        database = await PostgresTestDatabase.CreateAsync();
        context = database.CreateContext();
        userManager = TestIdentity.CreateUserManager(context);

        Seed();
    }

    [Fact]
    public async Task Search_finds_by_name_and_by_email()
    {
        var byName = await Service().SearchUsersAsync(new UserSearchQuery { Text = "Оксана" });
        var byEmail = await Service().SearchUsersAsync(new UserSearchQuery { Text = "person@" });

        // Адміністратор може знати будь-що з двох, тож шукаємо одразу в обох.
        Assert.Equal(PersonId, Assert.Single(byName.Items).Id);
        Assert.Equal(PersonId, Assert.Single(byEmail.Items).Id);
    }

    [Fact]
    public async Task Search_ignores_case()
    {
        var found = await Service().SearchUsersAsync(new UserSearchQuery { Text = "оКсАнА" });

        Assert.Single(found.Items);
    }

    [Fact]
    public async Task Banning_marks_the_account()
    {
        await Service().SetBannedAsync(PersonId, AdminId, isBanned: true);

        Assert.True(context.Users.Find(PersonId)!.IsBanned);

        await Service().SetBannedAsync(PersonId, AdminId, isBanned: false);

        Assert.False(context.Users.Find(PersonId)!.IsBanned);
    }

    [Fact]
    public async Task Banning_yourself_is_refused()
    {
        // Інакше адміністратор одним натисканням замикає двері зсередини.
        await Assert.ThrowsAsync<AdminActionException>(
            () => Service().SetBannedAsync(AdminId, AdminId, isBanned: true));
    }

    [Fact]
    public async Task Banning_someone_who_does_not_exist_is_refused()
    {
        await Assert.ThrowsAsync<UserNotFoundException>(
            () => Service().SetBannedAsync(999_999, AdminId, isBanned: true));
    }

    [Fact]
    public async Task A_moderator_can_be_appointed_and_dismissed()
    {
        // Саме так з'являються модератори: раніше роль існувала, але носіїв
        // у неї не було, і кожен новий вимагав правки конфігурації.
        await Service().SetRoleAsync(PersonId, AdminId, RoleNames.Moderator, granted: true);

        var appointed = await Service().SearchUsersAsync(new UserSearchQuery { Text = "person@" });
        Assert.Contains(RoleNames.Moderator, Assert.Single(appointed.Items).Roles);

        await Service().SetRoleAsync(PersonId, AdminId, RoleNames.Moderator, granted: false);

        var dismissed = await Service().SearchUsersAsync(new UserSearchQuery { Text = "person@" });
        Assert.DoesNotContain(RoleNames.Moderator, Assert.Single(dismissed.Items).Roles);
    }

    [Fact]
    public async Task Filtering_by_role_works()
    {
        await Service().SetRoleAsync(PersonId, AdminId, RoleNames.Moderator, granted: true);

        var moderators = await Service()
            .SearchUsersAsync(new UserSearchQuery { Role = RoleNames.Moderator });

        Assert.Equal(PersonId, Assert.Single(moderators.Items).Id);
    }

    [Fact]
    public async Task An_unknown_role_is_refused()
    {
        await Assert.ThrowsAsync<AdminActionException>(
            () => Service().SetRoleAsync(PersonId, AdminId, "Wizard", granted: true));
    }

    [Fact]
    public async Task Taking_the_admin_role_from_yourself_is_refused()
    {
        // Той самий спосіб замкнути двері зсередини, лише інший ключ.
        await Assert.ThrowsAsync<AdminActionException>(
            () => Service().SetRoleAsync(AdminId, AdminId, RoleNames.Admin, granted: false));
    }

    [Fact]
    public async Task Taking_the_admin_role_from_someone_else_is_allowed()
    {
        await Service().SetRoleAsync(OtherId, AdminId, RoleNames.Admin, granted: true);
        await Service().SetRoleAsync(OtherId, AdminId, RoleNames.Admin, granted: false);

        var found = await Service().SearchUsersAsync(new UserSearchQuery { Text = "other@" });

        Assert.DoesNotContain(RoleNames.Admin, Assert.Single(found.Items).Roles);
    }

    [Fact]
    public async Task Stats_count_what_matters()
    {
        await Service().SetBannedAsync(PersonId, AdminId, isBanned: true);

        var stats = await Service().GetStatsAsync();

        Assert.Equal(3, stats.TotalUsers);
        Assert.Equal(1, stats.BannedUsers);
    }

    public async Task DisposeAsync()
    {
        userManager.Dispose();
        await context.DisposeAsync();
        await database.DisposeAsync();
    }

    private AdminService Service() =>
        new(context, userManager, NullLogger<AdminService>.Instance);

    private void Seed()
    {
        foreach (var name in RoleNames.All)
        {
            context.Roles.Add(new Role { Name = name, NormalizedName = name.ToUpperInvariant() });
        }

        context.Users.Add(NewUser(AdminId, "admin", "Адміністратор"));
        context.Users.Add(NewUser(PersonId, "person", "Оксана Петренко"));
        context.Users.Add(NewUser(OtherId, "other", "Інша Людина"));

        context.SaveChanges();
    }

    private static User NewUser(long id, string login, string displayName) => new()
    {
        Id = id,
        UserName = $"{login}@example.com",
        NormalizedUserName = $"{login}@example.com".ToUpperInvariant(),
        Email = $"{login}@example.com",
        NormalizedEmail = $"{login}@example.com".ToUpperInvariant(),
        DisplayName = displayName,
        AccountType = AccountType.Private,

        // У житті печатку ставить Identity при реєстрації. Тут користувачі
        // кладуться в базу напряму, а без неї UserManager відмовляється
        // працювати: саме за нею він скасовує сесії при блокуванні.
        SecurityStamp = Guid.NewGuid().ToString("N"),
        ConcurrencyStamp = Guid.NewGuid().ToString("N"),
    };
}
