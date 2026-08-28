using AutoLot.Domain.Dealers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Dealers;

internal sealed class DealershipConfiguration : IEntityTypeConfiguration<Dealership>
{
    public void Configure(EntityTypeBuilder<Dealership> builder)
    {
        builder.ToTable("dealerships");

        builder.Property(dealership => dealership.Name)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(dealership => dealership.Slug)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(dealership => dealership.Description)
            .HasMaxLength(4000);

        builder.Property(dealership => dealership.LogoPath)
            .HasMaxLength(260);

        // Адреса сторінки салону мусить бути унікальною — інакше два салони
        // претендували б на той самий /dealers/avto-plus.
        builder.HasIndex(dealership => dealership.Slug).IsUnique();

        builder.HasOne(dealership => dealership.City)
            .WithMany()
            .HasForeignKey(dealership => dealership.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(dealership => dealership.VerifiedBy)
            .WithMany()
            .HasForeignKey(dealership => dealership.VerifiedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Головний запит вітрини: перевірені салони міста.
        builder.HasIndex(dealership => new { dealership.CityId, dealership.IsVerified });
    }
}

internal sealed class DealershipMemberConfiguration : IEntityTypeConfiguration<DealershipMember>
{
    public void Configure(EntityTypeBuilder<DealershipMember> builder)
    {
        builder.ToTable("dealership_members");

        // Складений ключ: одна людина не може бути в салоні двічі. Той самий
        // прийом, що у Favorite та CarFeature.
        builder.HasKey(member => new { member.DealershipId, member.UserId });

        builder.Property(member => member.Role)
            .IsRequired()
            .HasMaxLength(16)
            .HasConversion<string>();

        builder.HasOne(member => member.Dealership)
            .WithMany(dealership => dealership.Members)
            .HasForeignKey(member => member.DealershipId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(member => member.User)
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // «У яких салонах працює ця людина» — запит, який виконується на
        // кожній перевірці прав, тож без індексу він читав би всю таблицю.
        builder.HasIndex(member => member.UserId);
    }
}
