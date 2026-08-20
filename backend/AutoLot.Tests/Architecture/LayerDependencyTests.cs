using System.Reflection;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Common;
using AutoLot.Infrastructure.Persistence;

namespace AutoLot.Tests.Architecture;

/// <summary>
/// Стежить за напрямком залежностей із SPEC §2: Api → Application → Domain,
/// Infrastructure → Application → Domain. Порушення ловимо тестом, а не на рев'ю.
/// </summary>
public class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(Entity).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IDateTimeProvider).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(AutoLotDbContext).Assembly;

    [Theory]
    [InlineData("AutoLot.Application")]
    [InlineData("AutoLot.Infrastructure")]
    [InlineData("AutoLot.Api")]
    public void Domain_does_not_reference_outer_layers(string forbidden)
    {
        Assert.DoesNotContain(forbidden, ReferencesOf(DomainAssembly));
    }

    [Fact]
    public void Domain_does_not_reference_entity_framework()
    {
        Assert.DoesNotContain(
            ReferencesOf(DomainAssembly),
            reference => reference.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("AutoLot.Infrastructure")]
    [InlineData("AutoLot.Api")]
    public void Application_does_not_reference_outer_layers(string forbidden)
    {
        Assert.DoesNotContain(forbidden, ReferencesOf(ApplicationAssembly));
    }

    [Fact]
    public void Application_does_not_reference_entity_framework()
    {
        Assert.DoesNotContain(
            ReferencesOf(ApplicationAssembly),
            reference => reference.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_does_not_reference_api()
    {
        Assert.DoesNotContain("AutoLot.Api", ReferencesOf(InfrastructureAssembly));
    }

    private static IReadOnlyCollection<string> ReferencesOf(Assembly assembly)
    {
        return [.. assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty)];
    }
}
