using AutoLot.Domain.Auctions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Auctions;

internal sealed class AuctionConfiguration : IEntityTypeConfiguration<Auction>
{
    public void Configure(EntityTypeBuilder<Auction> builder)
    {
        builder.ToTable("auctions");

        builder.Property(auction => auction.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasConversion<string>();

        builder.Property(auction => auction.Status)
            .IsRequired()
            .HasMaxLength(16)
            .HasConversion<string>();

        // Один лот — одні торги. WithOne без колекції з боку оголошення:
        // навігація потрібна лише в цей бік, а зайве поле в Listing зробило б
        // його ще більшим без жодної користі.
        builder.HasOne(auction => auction.Listing)
            .WithOne()
            .HasForeignKey<Auction>(auction => auction.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Лідера не видаляють каскадом: користувача банять, а не стирають,
        // і історія торгів має лишитися цілою.
        builder.HasOne(auction => auction.Leader)
            .WithMany()
            .HasForeignKey(auction => auction.LeaderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(auction => auction.ListingId).IsUnique();

        // Головний запит планувальника: «які торги пора закривати».
        builder.HasIndex(auction => new { auction.Status, auction.EndsAt });
    }
}
