using AutoLot.Domain.Geo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Geo;

internal sealed class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.ToTable("regions");

        builder.Property(region => region.Code)
            .IsRequired()
            .HasMaxLength(16);

        // Унікальність коду — саме те, на що спирається ідемпотентний сід.
        builder.HasIndex(region => region.Code).IsUnique();
    }
}

internal sealed class RegionTranslationConfiguration : TranslationConfiguration<RegionTranslation>
{
    public override void Configure(EntityTypeBuilder<RegionTranslation> builder)
    {
        base.Configure(builder);

        builder.ToTable("region_translations");

        builder.HasOne(translation => translation.Region)
            .WithMany(region => region.Translations)
            .HasForeignKey(translation => translation.RegionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Один запис на пару «область + мова»: двох українських назв бути не може.
        builder.HasIndex(translation => new { translation.RegionId, translation.Language }).IsUnique();
    }
}
