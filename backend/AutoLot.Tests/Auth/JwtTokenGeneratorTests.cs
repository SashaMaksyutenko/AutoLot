using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Identity;
using AutoLot.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AutoLot.Tests.Auth;

public class JwtTokenGeneratorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    private readonly JwtTokenGenerator generator = new(
        Options.Create(new JwtOptions
        {
            Key = "тестовий-ключ-щонайменше-тридцять-два-символи",
            Issuer = "AutoLot",
            Audience = "AutoLot",
            AccessTokenMinutes = 15,
        }),
        new FixedClock(Now));

    [Fact]
    public void Puts_identity_and_roles_into_the_token()
    {
        var (token, _) = generator.Create(User(), [RoleNames.User, RoleNames.Moderator]);

        var parsed = new JsonWebTokenHandler().ReadJsonWebToken(token);

        Assert.Equal("77", parsed.GetClaim(AutoLotClaims.Subject).Value);
        Assert.Equal("driver@autolot.local", parsed.GetClaim(AutoLotClaims.Email).Value);
        Assert.Equal("Тарас", parsed.GetClaim(AutoLotClaims.Name).Value);
        Assert.Equal(nameof(AccountType.Dealer), parsed.GetClaim(AutoLotClaims.AccountType).Value);

        var roles = parsed.Claims
            .Where(claim => claim.Type == AutoLotClaims.Role)
            .Select(claim => claim.Value);

        Assert.Equal([RoleNames.User, RoleNames.Moderator], roles);
    }

    [Fact]
    public void Expires_after_the_configured_lifetime()
    {
        var (_, expiresAt) = generator.Create(User(), []);

        Assert.Equal(Now.AddMinutes(15), expiresAt);
    }

    [Fact]
    public void Issues_a_distinct_token_id_every_time()
    {
        var handler = new JsonWebTokenHandler();

        var first = handler.ReadJsonWebToken(generator.Create(User(), []).Token);
        var second = handler.ReadJsonWebToken(generator.Create(User(), []).Token);

        Assert.NotEqual(
            first.GetClaim(AutoLotClaims.TokenId).Value,
            second.GetClaim(AutoLotClaims.TokenId).Value);
    }

    [Fact]
    public void Signs_with_the_issuer_and_audience_from_options()
    {
        var parsed = new JsonWebTokenHandler().ReadJsonWebToken(generator.Create(User(), []).Token);

        Assert.Equal("AutoLot", parsed.Issuer);
        Assert.Contains("AutoLot", parsed.Audiences);
    }

    private static User User() => new()
    {
        Id = 77,
        Email = "driver@autolot.local",
        DisplayName = "Тарас",
        AccountType = AccountType.Dealer,
    };

    private sealed class FixedClock(DateTimeOffset now) : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => now;
    }
}
