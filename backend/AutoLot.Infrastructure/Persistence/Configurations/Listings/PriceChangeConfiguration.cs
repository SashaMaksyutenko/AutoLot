using AutoLot.Domain.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Listings;

internal sealed class PriceChangeConfiguration : IEntityTypeConfiguration<PriceChange>
{
    public void Configure(EntityTypeBuilder<PriceChange> builder)
    {
        builder.ToTable("price_changes");

        // Видалили оголошення — зникає й історія його ціни. Тримати записи
        // про те, чого більше немає, нема кому й нема навіщо.
        builder.HasOne(change => change.Listing)
            .WithMany()
            .HasForeignKey(change => change.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        /*
            Гроші зберігаємо як decimal із явною точністю, а не як число з
            плаваючою комою. Різниця не теоретична: 0.1 + 0.2 у double дає
            0.30000000000000004, і на сумах це рано чи пізно вилазить копійками,
            які нізвідки не беруться.

            18 цифр усього, 2 після коми — та сама точність, що й у ціні
            самого оголошення.
        */
        builder.Property(change => change.Price).HasPrecision(18, 2);
        builder.Property(change => change.PriceUah).HasPrecision(18, 2);

        // Валюту зберігаємо назвою, а не числом: у базі має бути видно «Usd»,
        // а не «1». Те саме рішення, що й для статусу оголошення.
        builder.Property(change => change.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasConversion<string>();

        // Єдиний запит, який буде: «історія цього оголошення за часом».
        builder.HasIndex(change => new { change.ListingId, change.ChangedAt });
    }
}
