using AutoLot.Domain.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Chat;

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");

        // Одна розмова на пару «оголошення + покупець». Без цього кожне
        // натискання «написати» починало б нову гілку, і листування
        // розсипалося б на десяток однакових.
        builder.HasIndex(item => new { item.ListingId, item.BuyerId }).IsUnique();

        builder.HasOne(item => item.Listing)
            .WithMany()
            .HasForeignKey(item => item.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Учасника не стирають каскадом: користувача банять, а листування
        // лишається — воно може знадобитися при розгляді скарги.
        builder.HasOne(item => item.Buyer)
            .WithMany()
            .HasForeignKey(item => item.BuyerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Головний запит списку: «мої розмови, найсвіжіші зверху».
        builder.HasIndex(item => new { item.BuyerId, item.LastMessageAt });
    }
}

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");

        builder.Property(item => item.Text)
            .IsRequired()
            .HasMaxLength(4000);

        builder.HasOne(item => item.Conversation)
            .WithMany(conversation => conversation.Messages)
            .HasForeignKey(item => item.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.Sender)
            .WithMany()
            .HasForeignKey(item => item.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Стрічка розмови й підрахунок непрочитаних — обидва йдуть по цій парі.
        builder.HasIndex(item => new { item.ConversationId, item.CreatedAt });
    }
}
