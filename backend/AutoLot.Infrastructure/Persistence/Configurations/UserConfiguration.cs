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

        // Restrict: довідник географії не має зникати «разом із» кимось, і
        // видалення міста, в якому є користувачі, має впасти з помилкою,
        // а не тихо занулити їм адресу.
        builder.HasOne(user => user.City)
            .WithMany()
            .HasForeignKey(user => user.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(user => user.CityDistrict)
            .WithMany()
            .HasForeignKey(user => user.CityDistrictId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(user => user.RefreshTokens)
            .WithOne(token => token.User)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
