using AutoLot.Domain.Geo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Geo;

internal sealed class CityDistrictConfiguration : IEntityTypeConfiguration<CityDistrict>
{
    public void Configure(EntityTypeBuilder<CityDistrict> builder)
    {
        builder.ToTable("city_districts");

        builder.Property(district => district.Code)
            .IsRequired()
            .HasMaxLength(32);

        builder.HasOne(district => district.City)
            .WithMany(city => city.CityDistricts)
            .HasForeignKey(district => district.CityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(district => district.Code).IsUnique();
    }
}

internal sealed class CityDistrictTranslationConfiguration : TranslationConfiguration<CityDistrictTranslation>
{
    public override void Configure(EntityTypeBuilder<CityDistrictTranslation> builder)
    {
        base.Configure(builder);

        builder.ToTable("city_district_translations");

        builder.HasOne(translation => translation.CityDistrict)
            .WithMany(district => district.Translations)
            .HasForeignKey(translation => translation.CityDistrictId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(translation => new { translation.CityDistrictId, translation.Language }).IsUnique();
    }
}
