using AutoLot.Infrastructure.Identity;

namespace AutoLot.Tests.Auth;

public class RefreshTokenFactoryTests
{
    [Fact]
    public void Generates_url_safe_values()
    {
        var token = RefreshTokenFactory.Generate();

        // Токен їде в cookie, тож '+', '/' і '=' там небажані.
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public void Generates_a_different_value_every_call()
    {
        var tokens = Enumerable.Range(0, 200)
            .Select(_ => RefreshTokenFactory.Generate())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(200, tokens.Count);
    }

    [Fact]
    public void Hashes_the_same_token_to_the_same_value()
    {
        var token = RefreshTokenFactory.Generate();

        Assert.Equal(RefreshTokenFactory.Hash(token), RefreshTokenFactory.Hash(token));
    }

    [Fact]
    public void Hash_does_not_reveal_the_token()
    {
        var token = RefreshTokenFactory.Generate();

        var hash = RefreshTokenFactory.Hash(token);

        Assert.NotEqual(token, hash);
        Assert.DoesNotContain(token, hash, StringComparison.Ordinal);
    }

    [Fact]
    public void Different_tokens_hash_differently()
    {
        Assert.NotEqual(
            RefreshTokenFactory.Hash(RefreshTokenFactory.Generate()),
            RefreshTokenFactory.Hash(RefreshTokenFactory.Generate()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuses_to_hash_nothing(string token)
    {
        Assert.Throws<ArgumentException>(() => RefreshTokenFactory.Hash(token));
    }
}
