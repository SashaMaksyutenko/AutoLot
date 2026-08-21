using AutoLot.Domain.Common;

namespace AutoLot.Tests.Geo;

public class LanguageCodesTests
{
    [Theory]
    [InlineData("uk", "uk")]
    [InlineData("en", "en")]
    [InlineData("EN", "en")]
    [InlineData("uk-UA", "uk")]
    [InlineData("en-GB", "en")]
    public void Normalizes_supported_languages(string input, string expected)
    {
        Assert.Equal(expected, LanguageCodes.Normalize(input));
    }

    [Theory]
    [InlineData("de")]
    [InlineData("pl-PL")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Falls_back_to_ukrainian_for_anything_else(string? input)
    {
        Assert.Equal(LanguageCodes.Ukrainian, LanguageCodes.Normalize(input));
    }

    [Fact]
    public void Recognises_only_the_two_supported_codes()
    {
        Assert.True(LanguageCodes.IsSupported("uk"));
        Assert.True(LanguageCodes.IsSupported("en"));
        Assert.False(LanguageCodes.IsSupported("de"));
        Assert.False(LanguageCodes.IsSupported(null));
    }
}
