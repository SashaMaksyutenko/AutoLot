using AutoLot.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;

namespace AutoLot.Tests.Infrastructure;

public class DatabaseConnectionTests
{
    [Fact]
    public void Resolve_returns_configured_connection_string()
    {
        const string expected = "Host=localhost;Port=5433;Database=autolot";

        var configuration = Build(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{DatabaseConnection.Name}"] = expected,
        });

        Assert.Equal(expected, DatabaseConnection.Resolve(configuration));
    }

    [Fact]
    public void Resolve_throws_with_actionable_message_when_missing()
    {
        var configuration = Build([]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => DatabaseConnection.Resolve(configuration));

        Assert.Contains(DatabaseConnection.Name, exception.Message, StringComparison.Ordinal);
        Assert.Contains("user-secrets", exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration Build(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
