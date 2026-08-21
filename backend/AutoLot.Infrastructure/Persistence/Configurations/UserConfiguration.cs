using AutoLot.Domain.Enums;
using AutoLot.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Типові імена Identity (AspNetUsers) у snake_case виглядають незграбно.
        builder.ToTable("users");

        builder.Property(user => user.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        // Тип акаунта тримаємо рядком: у дампі бази видно сенс, а не 0 чи 1.
        builder.Property(user => user.AccountType)
            .IsRequired()
            .HasMaxLength(16)
            .HasConversion<string>();

        builder.Property(user => user.IsBanned)
            .HasDefaultValue(false);

        builder.HasIndex(user => user.AccountType);

        builder.HasMany(user => user.RefreshTokens)
            .WithOne(token => token.User)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
