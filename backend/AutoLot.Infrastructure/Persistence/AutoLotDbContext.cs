using AutoLot.Domain.Cars;
using AutoLot.Domain.Common;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;
using AutoLot.Domain.Listings;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Persistence;

public class AutoLotDbContext(DbContextOptions<AutoLotDbContext> options)
    : IdentityDbContext<User, Role, long>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Region> Regions => Set<Region>();

    public DbSet<District> Districts => Set<District>();

    public DbSet<City> Cities => Set<City>();

    public DbSet<CityDistrict> CityDistricts => Set<CityDistrict>();

    public DbSet<EnumTranslation> EnumTranslations => Set<EnumTranslation>();

    public DbSet<Make> Makes => Set<Make>();

    public DbSet<Model> Models => Set<Model>();

    public DbSet<Generation> Generations => Set<Generation>();

    public DbSet<Country> Countries => Set<Country>();

    public DbSet<Feature> Features => Set<Feature>();

    public DbSet<Listing> Listings => Set<Listing>();

    public DbSet<Car> Cars => Set<Car>();

    public DbSet<CarPhoto> CarPhotos => Set<CarPhoto>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AutoLotDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Гроші — тільки decimal(18,2), див. SPEC §7.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
