using AutoLot.Domain.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Listings;

internal sealed class ListingQuestionConfiguration : IEntityTypeConfiguration<ListingQuestion>
{
    public void Configure(EntityTypeBuilder<ListingQuestion> builder)
    {
        builder.ToTable("listing_questions");

        builder.Property(question => question.Text)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(question => question.Answer)
            .HasMaxLength(2000);

        // Зникає оголошення — зникають і питання під ним: тримати розмову
        // про те, чого немає, немає сенсу.
        builder.HasOne(question => question.Listing)
            .WithMany()
            .HasForeignKey(question => question.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        // А от автора питання не видаляємо: користувача банять, а не стирають,
        // і публічна розмова має лишитися цілою.
        builder.HasOne(question => question.Asker)
            .WithMany()
            .HasForeignKey(question => question.AskerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Головний запит сторінки: усі питання цього лота, найновіші зверху.
        builder.HasIndex(question => new { question.ListingId, question.CreatedAt });
    }
}
