using AutoLot.Domain.Common;
using AutoLot.Infrastructure.Persistence.Configurations.Geo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Cars;

internal sealed class EnumTranslationConfiguration : TranslationConfiguration<EnumTranslation>
{
    public override void Configure(EntityTypeBuilder<EnumTranslation> builder)
    {
        base.Configure(builder);

        builder.ToTable("enum_translations");

        builder.Property(translation => translation.EnumName)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(translation => translation.Value)
            .IsRequired()
            .HasMaxLength(64);

        // Одна назва на трійку «перелічення + значення + мова».
        builder.HasIndex(translation => new
        {
            translation.EnumName,
            translation.Value,
            translation.Language,
        }).IsUnique();
    }
}
