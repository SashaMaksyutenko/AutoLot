using AutoLot.Application.Users.Dtos;
using AutoLot.Application.Users.Validation;

namespace AutoLot.Tests.Users;

/// <summary>
/// Межі того, що людина може змінити про себе. Найцікавіше тут — порожній
/// телефон: він дозволений, бо це спосіб номер прибрати.
/// </summary>
public class UpdateProfileRequestValidatorTests
{
    private readonly UpdateProfileRequestValidator validator = new();

    [Fact]
    public void A_name_and_a_valid_phone_pass()
    {
        var result = validator.Validate(Request("Оксана Петренко", "+380671234567"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void An_empty_phone_is_allowed()
    {
        // Це не «забули заповнити», а «прибрати номер».
        Assert.True(validator.Validate(Request("Оксана", null)).IsValid);
        Assert.True(validator.Validate(Request("Оксана", string.Empty)).IsValid);
        Assert.True(validator.Validate(Request("Оксана", "   ")).IsValid);
    }

    [Theory]
    [InlineData("0671234567")]
    [InlineData("+38067123456")]
    [InlineData("+3806712345678")]
    [InlineData("+380abcdefghi")]
    [InlineData("+79161234567")]
    public void A_phone_in_the_wrong_shape_is_refused(string phone)
    {
        var result = validator.Validate(Request("Оксана", phone));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "PhoneNumber");
    }

    [Theory]
    [InlineData("")]
    [InlineData("О")]
    public void A_name_that_is_too_short_is_refused(string name)
    {
        Assert.False(validator.Validate(Request(name, null)).IsValid);
    }

    [Fact]
    public void A_name_that_is_too_long_is_refused()
    {
        Assert.False(validator.Validate(Request(new string('О', 101), null)).IsValid);
    }

    private static UpdateProfileRequest Request(string name, string? phone) => new()
    {
        DisplayName = name,
        PhoneNumber = phone,
    };
}
