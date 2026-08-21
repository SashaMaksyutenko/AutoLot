using AutoLot.Domain.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Persistence;

public class AutoLotDbContext(DbContextOptions<AutoLotDbContext> options)
    : IdentityDbContext<User, Role, long>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

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
