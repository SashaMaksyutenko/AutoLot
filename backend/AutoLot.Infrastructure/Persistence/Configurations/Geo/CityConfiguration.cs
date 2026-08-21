using AutoLot.Domain.Geo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Geo;

internal sealed class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("cities");

        builder.Property(city => city.Code)
            .IsRequired()
            .HasMaxLength(24);

        builder.HasOne(city => city.Region)
            .WithMany(region => region.Cities)
            .HasForeignKey(city => city.RegionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, а не Cascade: якщо район прибирають, міста мають лишитися,
        // а не зникнути разом із ним. Спершу їх треба перепідпорядкувати.
        builder.HasOne(city => city.District)
            .WithMany(district => district.Cities)
            .HasForeignKey(city => city.DistrictId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(city => city.Code).IsUnique();

        // Списки міст завжди фільтруються за областю й сортуються за розміром.
        builder.HasIndex(city => new { city.RegionId, city.Population });
        builder.HasIndex(city => city.DistrictId);
    }
}

internal sealed class CityTranslationConfiguration : TranslationConfiguration<CityTranslation>
{
    public override void Configure(EntityTypeBuilder<CityTranslation> builder)
    {
        base.Configure(builder);

        builder.ToTable("city_translations");

        builder.HasOne(translation => translation.City)
            .WithMany(city => city.Translations)
            .HasForeignKey(translation => translation.CityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(translation => new { translation.CityId, translation.Language }).IsUnique();
    }
}
