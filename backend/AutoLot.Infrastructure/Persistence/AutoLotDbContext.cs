using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Persistence;

public class AutoLotDbContext(DbContextOptions<AutoLotDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AutoLotDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Гроші — тільки decimal(18,2), див. SPEC §7.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
