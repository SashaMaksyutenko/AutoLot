using AutoLot.Domain.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Listings;

internal sealed class ListingViewConfiguration : IEntityTypeConfiguration<ListingView>
{
    public void Configure(EntityTypeBuilder<ListingView> builder)
    {
        builder.ToTable("listing_views");

        builder.HasOne(view => view.User)
            .WithMany()
            .HasForeignKey(view => view.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Видалили оголошення — зникає й згадка про його перегляд. Історія з
        // рядками, які нікуди не ведуть, гірша за коротшу історію.
        builder.HasOne(view => view.Listing)
            .WithMany()
            .HasForeignKey(view => view.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Одна пара — один рядок. Саме це обмеження й робить історію
        // переліком авто, а не переліком кліків.
        builder.HasIndex(view => new { view.UserId, view.ListingId }).IsUnique();

        // Головний запит: «моя історія, найсвіжіше зверху».
        builder.HasIndex(view => new { view.UserId, view.ViewedAt });
    }
}
