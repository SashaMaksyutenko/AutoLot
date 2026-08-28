using AutoLot.Application.Dealers;
using AutoLot.Application.Dealers.Dtos;
using AutoLot.Domain.Common;
using AutoLot.Domain.Dealers;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;
using AutoLot.Infrastructure.Dealers;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Tests.TestDoubles;

namespace AutoLot.Tests.Dealers;

/// <summary>
/// Салон і його персонал. Головне, що тут перевіряється, — межа між тим, що
/// може менеджер, і тим, що може лише власник.
/// </summary>
public class DealershipServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    private const long OwnerId = 1;

    private const long ManagerId = 2;

    private const long OutsiderId = 3;

    private const long ModeratorId = 4;

    private readonly TestDatabase database = new();

    private readonly AutoLotDbContext context;

    public DealershipServiceTests()
    {
        context = database.CreateContext();
        SeedUsers();
    }

    [Fact]
    public async Task The_founder_becomes_the_owner()
    {
        var created = await Service().CreateAsync(OwnerId, Request("Авто Плюс"));

        var memberships = await Service().GetMembershipsAsync(OwnerId);

        var membership = Assert.Single(memberships);
        Assert.Equal(created.Id, membership.DealershipId);

        // Інакше салон лишився б без нікого, хто може додати персонал.
        Assert.Equal(DealershipRole.Owner, membership.Role);
    }

    [Fact]
    public async Task Founding_turns_the_account_into_a_dealer()
    {
        await Service().CreateAsync(OwnerId, Request("Авто Плюс"));

        // Ліміт у п'ять оголошень до салону не діє, тож тип акаунта має
        // збігатися з дійсністю.
        Assert.Equal(AccountType.Dealer, context.Users.Find(OwnerId)!.AccountType);
    }

    [Fact]
    public async Task The_name_becomes_a_readable_address()
    {
        var created = await Service().CreateAsync(OwnerId, Request("Авто Плюс"));

        Assert.Equal("avto-plyus", created.Slug);
    }

    [Fact]
    public async Task Two_salons_with_the_same_name_get_different_addresses()
    {
        var first = await Service().CreateAsync(OwnerId, Request("Авто Плюс"));
        var second = await Service().CreateAsync(ManagerId, Request("Авто Плюс"));

        // Однакові назви в різних містах цілком можливі, а адреса мусить
        // лишатися унікальною.
        Assert.Equal("avto-plyus", first.Slug);
        Assert.Equal("avto-plyus-2", second.Slug);
    }

    [Fact]
    public async Task The_owner_hires_and_the_newcomer_becomes_a_manager()
    {
        var created = await Service().CreateAsync(OwnerId, Request("Авто Плюс"));

        await Service().AddStaffAsync(created.Id, OwnerId, "manager@example.com", DealershipRole.Manager);

        var staff = await Service().GetStaffAsync(created.Id, OwnerId);

        Assert.Equal(2, staff.Count);
        Assert.Contains(staff, member => member.UserId == ManagerId && member.Role == DealershipRole.Manager);
    }

    [Fact]
    public async Task A_manager_cannot_hire()
    {
        var created = await Service().CreateAsync(OwnerId, Request("Авто Плюс"));
        await Service().AddStaffAsync(created.Id, OwnerId, "manager@example.com", DealershipRole.Manager);

        // Уся різниця між ролями саме тут: менеджер веде оголошення, склад
        // персоналу вирішує власник.
        await Assert.ThrowsAsync<DealershipAccessException>(
            () => Service().AddStaffAsync(created.Id, ManagerId, "outsider@example.com", DealershipRole.Manager));
    }

    [Fact]
    public async Task An_outsider_cannot_even_see_the_staff()
    {
        var created = await Service().CreateAsync(OwnerId, Request("Авто Плюс"));

        // У списку персоналу є пошта живих людей — це не публічні дані.
        await Assert.ThrowsAsync<DealershipAccessException>(
            () => Service().GetStaffAsync(created.Id, OutsiderId));
    }

    [Fact]
    public async Task The_same_person_cannot_be_hired_twice()
    {
        var created = await Service().CreateAsync(OwnerId, Request("Авто Плюс"));
        await Service().AddStaffAsync(created.Id, OwnerId, "manager@example.com", DealershipRole.Manager);

        await Assert.ThrowsAsync<DomainRuleException>(
            () => Service().AddStaffAsync(created.Id, OwnerId, "manager@example.com", DealershipRole.Manager));
    }

    [Fact]
    public async Task Hiring_someone_who_does_not_exist_is_refused()
    {
        var created = await Service().CreateAsync(OwnerId, Request("Авто Плюс"));

        await Assert.ThrowsAsync<DomainRuleException>(
            () => Service().AddStaffAsync(created.Id, OwnerId, "nobody@example.com", DealershipRole.Manager));
    }

    [Fact]
    public async Task The_last_owner_cannot_be_removed()
    {
        var created = await Service().CreateAsync(OwnerId, Request("Авто Плюс"));

        // Салон лишився б без нікого, хто може керувати персоналом.
        await Assert.ThrowsAsync<DomainRuleException>(
            () => Service().RemoveStaffAsync(created.Id, OwnerId, OwnerId));
    }

    [Fact]
    public async Task A_manager_can_be_dismissed()
    {
        var created = await Service().CreateAsync(OwnerId, Request("Авто Плюс"));
        await Service().AddStaffAsync(created.Id, OwnerId, "manager@example.com", DealershipRole.Manager);

        await Service().RemoveStaffAsync(created.Id, OwnerId, ManagerId);

        var staff = await Service().GetStaffAsync(created.Id, OwnerId);
        Assert.Single(staff);
    }

    [Fact]
    public async Task A_moderator_puts_the_verified_badge_and_can_take_it_back()
    {
        var created = await Service().CreateAsync(OwnerId, Request("Авто Плюс"));

        await Service().SetVerificationAsync(created.Id, ModeratorId, isVerified: true);

        var verified = await Service().GetBySlugAsync(created.Slug);
        Assert.True(verified!.IsVerified);
        Assert.Equal(Now, verified.VerifiedAt);

        await Service().SetVerificationAsync(created.Id, ModeratorId, isVerified: false);

        var revoked = await Service().GetBySlugAsync(created.Slug);
        Assert.False(revoked!.IsVerified);

        // Слід про те, хто перевіряв, лишається навмисно: історія рішень
        // цінніша за чистоту полів.
        Assert.Equal(ModeratorId, context.Dealerships.Find(created.Id)!.VerifiedById);
    }

    [Fact]
    public async Task A_salon_in_an_unknown_city_is_refused()
    {
        await Assert.ThrowsAsync<DomainRuleException>(
            () => Service().CreateAsync(OwnerId, Request("Авто Плюс") with { CityId = 999 }));
    }

    [Fact]
    public async Task An_unknown_address_gives_nothing()
    {
        Assert.Null(await Service().GetBySlugAsync("nema-takogo"));
    }

    public void Dispose()
    {
        context.Dispose();
        database.Dispose();
        GC.SuppressFinalize(this);
    }

    private DealershipService Service() =>
        new(context, new FixedClock(Now), new StubLanguage());

    private static CreateDealershipRequest Request(string name) => new()
    {
        Name = name,
        CityId = 1,
    };

    private void SeedUsers()
    {
        context.Regions.Add(new Region { Id = 1, Code = "kyiv-region" });
        context.Cities.Add(new City { Id = 1, RegionId = 1, Code = "kyiv" });

        context.Users.Add(NewUser(OwnerId, "owner", "Власник"));
        context.Users.Add(NewUser(ManagerId, "manager", "Менеджер"));
        context.Users.Add(NewUser(OutsiderId, "outsider", "Сторонній"));
        context.Users.Add(NewUser(ModeratorId, "moderator", "Модератор"));

        context.SaveChanges();
    }

    private static User NewUser(long id, string login, string displayName) => new()
    {
        Id = id,
        UserName = $"{login}@example.com",
        Email = $"{login}@example.com",

        // NormalizedEmail заповнюємо самі: у житті це робить Identity, а тут
        // користувачі кладуться в базу напряму, і пошук за поштою без нього
        // нічого б не знайшов.
        NormalizedEmail = $"{login}@example.com".ToUpperInvariant(),
        DisplayName = displayName,
    };
}
