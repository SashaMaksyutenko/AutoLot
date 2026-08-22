using AutoLot.Domain.Geo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Geo;

internal sealed class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("countries");

        builder.Property(country => country.Code)
            .IsRequired()
            .HasMaxLength(2)
            .IsFixedLength();

        builder.HasIndex(country => country.Code).IsUnique();
    }
}

internal sealed class CountryTranslationConfiguration : TranslationConfiguration<CountryTranslation>
{
    public override void Configure(EntityTypeBuilder<CountryTranslation> builder)
    {
        base.Configure(builder);

        builder.ToTable("country_translations");

        builder.HasOne(translation => translation.Country)
            .WithMany(country => country.Translations)
            .HasForeignKey(translation => translation.CountryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(translation => new { translation.CountryId, translation.Language }).IsUnique();
    }
}
