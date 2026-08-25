using AutoLot.Domain.Auctions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Auctions;

internal sealed class BidConfiguration : IEntityTypeConfiguration<Bid>
{
    public void Configure(EntityTypeBuilder<Bid> builder)
    {
        builder.ToTable("bids");

        builder.HasOne(bid => bid.Auction)
            .WithMany(auction => auction.Bids)
            .HasForeignKey(bid => bid.AuctionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(bid => bid.Bidder)
            .WithMany()
            .HasForeignKey(bid => bid.BidderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Історію завжди читають одним способом: усі ставки лота, найновіші
        // зверху. Спадний порядок прямо в індексі позбавляє базу сортування.
        builder.HasIndex(bid => new { bid.AuctionId, bid.CreatedAt })
            .IsDescending(false, true);
    }
}
