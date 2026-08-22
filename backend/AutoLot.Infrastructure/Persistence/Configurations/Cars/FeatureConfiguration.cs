using AutoLot.Domain.Cars;
using AutoLot.Infrastructure.Persistence.Configurations.Geo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Cars;

internal sealed class FeatureConfiguration : IEntityTypeConfiguration<Feature>
{
    public void Configure(EntityTypeBuilder<Feature> builder)
    {
        builder.ToTable("features");

        builder.Property(feature => feature.Code)
            .IsRequired()
            .HasMaxLength(64);

        // Рядком, а не числом: у дампі бази одразу видно «Safety», а не «2».
        builder.Property(feature => feature.Category)
            .IsRequired()
            .HasMaxLength(24)
            .HasConversion<string>();

        builder.HasIndex(feature => feature.Code).IsUnique();
        builder.HasIndex(feature => new { feature.Category, feature.SortOrder });
    }
}

internal sealed class FeatureTranslationConfiguration : TranslationConfiguration<FeatureTranslation>
{
    public override void Configure(EntityTypeBuilder<FeatureTranslation> builder)
    {
        base.Configure(builder);

        builder.ToTable("feature_translations");

        builder.HasOne(translation => translation.Feature)
            .WithMany(feature => feature.Translations)
            .HasForeignKey(translation => translation.FeatureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(translation => new { translation.FeatureId, translation.Language }).IsUnique();
    }
}
