using AutoLot.Domain.Geo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Geo;

internal sealed class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> builder)
    {
        builder.ToTable("districts");

        builder.Property(district => district.Code)
            .IsRequired()
            .HasMaxLength(24);

        builder.HasOne(district => district.Region)
            .WithMany(region => region.Districts)
            .HasForeignKey(district => district.RegionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(district => district.Code).IsUnique();
    }
}

internal sealed class DistrictTranslationConfiguration : TranslationConfiguration<DistrictTranslation>
{
    public override void Configure(EntityTypeBuilder<DistrictTranslation> builder)
    {
        base.Configure(builder);

        builder.ToTable("district_translations");

        builder.HasOne(translation => translation.District)
            .WithMany(district => district.Translations)
            .HasForeignKey(translation => translation.DistrictId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(translation => new { translation.DistrictId, translation.Language }).IsUnique();
    }
}
