using AutoLot.Application.Users.Dtos;
using AutoLot.Application.Users.Validation;

namespace AutoLot.Tests.Users;

public class UpdateLocationRequestValidatorTests
{
    private readonly UpdateLocationRequestValidator validator = new();

    [Fact]
    public void Accepts_city_with_district()
    {
        Assert.True(validator.Validate(new UpdateLocationRequest(1, 5)).IsValid);
    }

    [Fact]
    public void Accepts_city_without_district()
    {
        Assert.True(validator.Validate(new UpdateLocationRequest(1, null)).IsValid);
    }

    [Fact]
    public void Accepts_empty_request_as_clearing_the_location()
    {
        Assert.True(validator.Validate(new UpdateLocationRequest(null, null)).IsValid);
    }

    [Fact]
    public void Rejects_district_without_city()
    {
        var result = validator.Validate(new UpdateLocationRequest(null, 5));

        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(UpdateLocationRequest.CityDistrictId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_non_positive_identifiers(long cityId)
    {
        var result = validator.Validate(new UpdateLocationRequest(cityId, null));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateLocationRequest.CityId));
    }
}
