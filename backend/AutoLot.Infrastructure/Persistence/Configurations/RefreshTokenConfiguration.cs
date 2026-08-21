using AutoLot.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        // base64 від SHA-256 — рівно 44 символи.
        builder.Property(token => token.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(token => token.RevokedReason)
            .HasMaxLength(200);

        builder.Property(token => token.CreatedByIp)
            .HasMaxLength(45);

        builder.HasIndex(token => token.TokenHash).IsUnique();

        // Гасіння сім'ї при повторному використанні йде саме цим індексом.
        builder.HasIndex(token => new { token.UserId, token.FamilyId });
    }
}
