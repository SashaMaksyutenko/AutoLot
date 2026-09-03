using AutoLot.Application.Billing;
using AutoLot.Domain.Billing;
using AutoLot.Domain.Cars;
using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Billing;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoLot.Tests.Billing;

/// <summary>
/// Гаманець і підписки на базі.
///
/// Головне, що тут перевіряється, — неподільність оплати: списання й
/// оформлення або стаються разом, або не стаються зовсім. Списані кошти
/// без підписки — найгірше, що може дати такий механізм.
/// </summary>
public class BillingServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

    private const long PersonId = 1;

    private const long DealerId = 2;

    private readonly TestDatabase database = new();

    private readonly AutoLotDbContext context;

    public BillingServiceTests()
    {
        context = database.CreateContext();
        Seed();
    }

    [Fact]
    public async Task A_new_wallet_starts_empty()
    {
        var wallet = await Service().GetWalletAsync(PersonId);

        Assert.Equal(0m, wallet.Balance);
        Assert.Empty(wallet.Recent);
    }

    [Fact]
    public async Task The_wallet_appears_only_when_first_needed()
    {
        Assert.Empty(context.Wallets);

        await Service().GetWalletAsync(PersonId);

        // Заводити гаманець кожному при реєстрації означало б таблицю
        // розміром із таблицю користувачів, майже всю з нулями.
        Assert.Single(context.Wallets);
    }

    [Fact]
    public async Task A_top_up_shows_up_in_the_history()
    {
        await Service().TopUpAsync(PersonId, 500m);

        var wallet = await Service().GetWalletAsync(PersonId);

        Assert.Equal(500m, wallet.Balance);

        var entry = Assert.Single(wallet.Recent);
        Assert.Equal(500m, entry.Amount);
        Assert.Equal(WalletOperation.TopUp, entry.Kind);
    }

    [Fact]
    public async Task An_absurd_top_up_is_refused()
    {
        // Захист від зайвого нуля, а не від шахрайства: справжніх грошей
        // тут немає взагалі.
        await Assert.ThrowsAsync<DomainRuleException>(
            () => Service().TopUpAsync(PersonId, 1_000_000m));
    }

    [Fact]
    public async Task Without_paying_anything_the_free_plan_applies()
    {
        var state = await Service().GetSubscriptionAsync(PersonId);

        Assert.Equal("free", state.Plan.Code);
        Assert.Equal(5, state.Plan.ListingLimit);

        // Безкоштовний тариф безстроковий, тож дати закінчення немає.
        Assert.Null(state.ActiveUntil);
    }

    [Fact]
    public async Task Subscribing_charges_the_wallet_and_raises_the_limit()
    {
        await Service().TopUpAsync(PersonId, 500m);

        var state = await Service().SubscribeAsync(PersonId, "plus");

        Assert.Equal("plus", state.Plan.Code);
        Assert.Equal(20, state.Plan.ListingLimit);
        Assert.Equal(Now.AddDays(30), state.ActiveUntil);

        var wallet = await Service().GetWalletAsync(PersonId);
        Assert.Equal(351m, wallet.Balance);
        Assert.Equal(20, await Service().GetListingLimitAsync(PersonId));
    }

    [Fact]
    public async Task Without_money_nothing_happens_at_all()
    {
        await Service().TopUpAsync(PersonId, 100m);

        await Assert.ThrowsAsync<InsufficientFundsException>(
            () => Service().SubscribeAsync(PersonId, "plus"));

        // Ані списання, ані підписки: або все, або нічого.
        Assert.Equal(100m, (await Service().GetWalletAsync(PersonId)).Balance);
        Assert.Empty(context.Subscriptions);
        Assert.Equal(5, await Service().GetListingLimitAsync(PersonId));
    }

    [Fact]
    public async Task The_free_plan_is_not_for_sale()
    {
        await Service().TopUpAsync(PersonId, 500m);

        // Дозволити купівлю означало б продавати нуль.
        await Assert.ThrowsAsync<SubscriptionNotAllowedException>(
            () => Service().SubscribeAsync(PersonId, "free"));
    }

    [Fact]
    public async Task An_unknown_plan_is_refused()
    {
        await Assert.ThrowsAsync<PlanNotFoundException>(
            () => Service().SubscribeAsync(PersonId, "platinum"));
    }

    [Fact]
    public async Task Renewal_adds_to_the_paid_period()
    {
        await Service().TopUpAsync(PersonId, 500m);
        await Service().SubscribeAsync(PersonId, "plus");

        var state = await ServiceAt(Now.AddDays(10)).SubscribeAsync(PersonId, "plus");

        // Продовжили на десятий день — строк рахується від кінця першого
        // періоду, а не від сьогодні.
        Assert.Equal(Now.AddDays(60), state.ActiveUntil);
        Assert.Equal(202m, (await Service().GetWalletAsync(PersonId)).Balance);
    }

    [Fact]
    public async Task Switching_plans_mid_period_is_refused()
    {
        await Service().TopUpAsync(PersonId, 1_000m);
        await Service().SubscribeAsync(PersonId, "plus");

        // Перехід посеред оплаченого періоду вимагав би перерахунку — це вже
        // тарифікація, якої проєкт свідомо не має.
        await Assert.ThrowsAsync<SubscriptionNotAllowedException>(
            () => Service().SubscribeAsync(PersonId, "pro"));
    }

    [Fact]
    public async Task When_the_period_ends_the_free_plan_returns()
    {
        await Service().TopUpAsync(PersonId, 500m);
        await Service().SubscribeAsync(PersonId, "plus");

        var later = ServiceAt(Now.AddDays(31));

        Assert.Equal("free", (await later.GetSubscriptionAsync(PersonId)).Plan.Code);
        Assert.Equal(5, await later.GetListingLimitAsync(PersonId));
    }

    [Fact]
    public async Task The_top_plan_means_no_cap_at_all()
    {
        await Service().TopUpAsync(PersonId, 500m);
        await Service().SubscribeAsync(PersonId, "pro");

        // null — це «без межі», а не «нуль оголошень».
        Assert.Null(await Service().GetListingLimitAsync(PersonId));
    }

    [Fact]
    public async Task A_dealer_account_is_unlimited_whatever_the_plan()
    {
        // SPEC §3: вітрина салону без оголошень не має сенсу.
        Assert.Null(await Service().GetListingLimitAsync(DealerId));
    }

    [Fact]
    public async Task The_used_count_matches_what_the_limit_counts()
    {
        NewListing(ListingStatus.Active);
        NewListing(ListingStatus.PendingModeration);
        NewListing(ListingStatus.Draft);
        NewListing(ListingStatus.Archived);

        var state = await Service().GetSubscriptionAsync(PersonId);

        // Інакше «3 з 5» у кабінеті розходилося б із відмовою при публікації.
        Assert.Equal(2, state.ActiveListings);
    }

    [Fact]
    public async Task The_plan_list_marks_the_current_one()
    {
        await Service().TopUpAsync(PersonId, 500m);
        await Service().SubscribeAsync(PersonId, "plus");

        var plans = await Service().GetPlansAsync(PersonId);

        Assert.Equal(3, plans.Count);
        Assert.Equal("plus", plans.Single(plan => plan.IsCurrent).Code);
    }

    [Fact]
    public async Task For_a_guest_no_plan_is_current()
    {
        var plans = await Service().GetPlansAsync(userId: null);

        Assert.Equal(3, plans.Count);
        Assert.DoesNotContain(plans, plan => plan.IsCurrent);
    }

    [Fact]
    public async Task Names_come_from_the_reference_not_from_code()
    {
        var plans = await Service().GetPlansAsync(userId: null);

        Assert.Equal("Базовий", plans[0].Name);
        Assert.Equal("Плюс", plans[1].Name);
    }

    public void Dispose()
    {
        context.Dispose();
        database.Dispose();
        GC.SuppressFinalize(this);
    }

    private BillingService Service() => ServiceAt(Now);

    private BillingService ServiceAt(DateTimeOffset now) =>
        new(context, new FixedClock(now), new StubLanguage(), NullLogger<BillingService>.Instance);

    private void NewListing(ListingStatus status)
    {
        context.Listings.Add(new Listing
        {
            Title = "Тестовий лот",
            Description = "Опис",
            SellerId = PersonId,
            CityId = 1,
            Price = 10_000m,
            Currency = Currency.Usd,
            PriceUah = 420_000m,
            Status = status,
            Car = new Car
            {
                Year = 2020,
                MakeId = 1,
                ModelId = 1,
                FuelType = FuelType.Petrol,
                Transmission = TransmissionType.Manual,
                Drivetrain = DrivetrainType.FrontWheel,
                BodyType = BodyType.Sedan,
                Color = CarColor.Black,
            },
        });

        context.SaveChanges();
    }

    /// <summary>
    /// Плани заводимо тими самими, що в plans.json: тест на ліміти інакше
    /// перевіряв би вигадані числа замість справжнього довідника.
    /// </summary>
    private void Seed()
    {
        context.Regions.Add(new Region { Id = 1, Code = "kyiv-region" });
        context.Cities.Add(new City { Id = 1, RegionId = 1, Code = "kyiv" });
        context.Makes.Add(new Make { Id = 1, Name = "BMW", Slug = "bmw" });
        context.Models.Add(new Model { Id = 1, MakeId = 1, Name = "X5", Slug = "x5" });

        context.Users.Add(new User
        {
            Id = PersonId,
            UserName = "person@example.com",
            Email = "person@example.com",
            DisplayName = "Приватна особа",
            AccountType = AccountType.Private,
        });

        context.Users.Add(new User
        {
            Id = DealerId,
            UserName = "dealer@example.com",
            Email = "dealer@example.com",
            DisplayName = "Салон",
            AccountType = AccountType.Dealer,
        });

        AddPlan(1, "free", 0m, 5, isDefault: true, "Базовий", 0);
        AddPlan(2, "plus", 149m, 20, isDefault: false, "Плюс", 1);
        AddPlan(3, "pro", 399m, null, isDefault: false, "Профі", 2);

        context.SaveChanges();
    }

    private void AddPlan(
        long id,
        string code,
        decimal price,
        int? limit,
        bool isDefault,
        string name,
        int sortOrder)
    {
        var plan = new Plan
        {
            Id = id,
            Code = code,
            Price = price,
            DurationDays = 30,
            ListingLimit = limit,
            IsDefault = isDefault,
            SortOrder = sortOrder,
        };

        plan.Translations.Add(new PlanTranslation
        {
            PlanId = id,
            Language = LanguageCodes.Ukrainian,
            Name = name,
            Description = "Опис",
        });

        context.Plans.Add(plan);
    }
}
