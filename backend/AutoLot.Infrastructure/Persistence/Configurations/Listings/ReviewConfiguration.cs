using AutoLot.Domain.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Listings;

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("reviews");

        builder.Property(review => review.Text).HasMaxLength(1000);

        // Один відгук на угоду від однієї людини. Саме індексом, а не лише
        // перевіркою в коді: два одночасні запити пройшли б перевірку обидва,
        // а база другий не пропустить.
        builder.HasIndex(review => new { review.ListingId, review.AuthorId }).IsUnique();

        // Рейтинг людини: «усі відгуки про неї, найновіші зверху».
        builder.HasIndex(review => new { review.SubjectId, review.CreatedAt });

        builder.HasOne(review => review.Listing)
            .WithMany()
            .HasForeignKey(review => review.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Людей не стирають каскадом: відгук — свідчення про угоду, і
        // зникати разом з акаунтом він не повинен.
        builder.HasOne(review => review.Author)
            .WithMany()
            .HasForeignKey(review => review.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(review => review.Subject)
            .WithMany()
            .HasForeignKey(review => review.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
