using AutoLot.Application.Auth.Dtos;
using AutoLot.Application.Auth.Validation;
using AutoLot.Domain.Enums;

namespace AutoLot.Tests.Auth;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator validator = new();

    [Fact]
    public void Accepts_a_well_formed_request()
    {
        var result = validator.Validate(Request());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("не-пошта")]
    [InlineData("without@")]
    public void Rejects_malformed_email(string email)
    {
        var result = validator.Validate(Request() with { Email = email });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterRequest.Email));
    }

    [Theory]
    [InlineData("short1A", "закороткий")]
    [InlineData("alllowercase1", "без великої літери")]
    [InlineData("ALLUPPERCASE1", "без малої літери")]
    [InlineData("NoDigitsHere", "без цифри")]
    public void Rejects_password_that_breaks_the_policy(string password, string reason)
    {
        var result = validator.Validate(Request() with { Password = password });

        Assert.False(result.IsValid, $"Пароль мав бути відхилений: {reason}");
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterRequest.Password));
    }

    [Theory]
    [InlineData("+380501234567")]
    [InlineData(null)]
    [InlineData("")]
    public void Accepts_ukrainian_phone_or_none(string? phone)
    {
        var result = validator.Validate(Request() with { PhoneNumber = phone });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("0501234567")]
    [InlineData("+38050123456")]
    [InlineData("+1 555 000 11 22")]
    public void Rejects_phone_in_other_formats(string phone)
    {
        var result = validator.Validate(Request() with { PhoneNumber = phone });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterRequest.PhoneNumber));
    }

    [Fact]
    public void Rejects_unknown_account_type()
    {
        var result = validator.Validate(Request() with { AccountType = (AccountType)42 });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterRequest.AccountType));
    }

    private static RegisterRequest Request() => new(
        "driver@autolot.local",
        "Sich2026!",
        "Тарас",
        AccountType.Private,
        "+380501234567");
}
