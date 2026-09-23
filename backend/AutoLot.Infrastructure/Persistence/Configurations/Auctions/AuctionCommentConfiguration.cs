using AutoLot.Domain.Auctions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Auctions;

internal sealed class AuctionCommentConfiguration : IEntityTypeConfiguration<AuctionComment>
{
    public void Configure(EntityTypeBuilder<AuctionComment> builder)
    {
        builder.ToTable("auction_comments");

        builder.Property(comment => comment.Text)
            .IsRequired()
            .HasMaxLength(AuctionComment.MaxLength);

        // Видалили оголошення — зникає й розмова під ним.
        builder.HasOne(comment => comment.Listing)
            .WithMany()
            .HasForeignKey(comment => comment.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        /*
            А от автора видаляти разом із його коментарями не можна: це
            вирвало б репліки з середини розмови, і відповіді на них повисли
            б у повітрі. Restrict змушує спершу вирішити, що з ними робити.
        */
        builder.HasOne(comment => comment.Author)
            .WithMany()
            .HasForeignKey(comment => comment.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Головний запит: «коментарі цього лота, свіжіші зверху».
        builder.HasIndex(comment => new { comment.ListingId, comment.CreatedAt });

        // І для паузи між коментарями: «коли ця людина писала тут востаннє».
        builder.HasIndex(comment => new { comment.ListingId, comment.AuthorId, comment.CreatedAt });
    }
}
