using AutoLot.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Geo;

/// <summary>
/// Спільні правила для всіх таблиць перекладів. Спадкоємці додають лише те,
/// що відрізняється — зв'язок із власною сутністю.
/// </summary>
internal abstract class TranslationConfiguration<TTranslation> : IEntityTypeConfiguration<TTranslation>
    where TTranslation : Translation
{
    public virtual void Configure(EntityTypeBuilder<TTranslation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // "uk", "en" — з запасом на випадок кодів на кшталт "uk-UA".
        builder.Property(translation => translation.Language)
            .IsRequired()
            .HasMaxLength(5);

        builder.Property(translation => translation.Name)
            .IsRequired()
            .HasMaxLength(120);

        // Пошук довідників іде саме за назвою.
        builder.HasIndex(translation => translation.Name);
    }
}
