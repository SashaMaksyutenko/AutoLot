using AutoLot.Domain.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Listings;

internal sealed class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.ToTable("listings");

        builder.Property(listing => listing.Title)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(listing => listing.Description)
            .IsRequired()
            .HasMaxLength(5000);

        builder.Property(listing => listing.RejectionReason)
            .HasMaxLength(500);

        builder.Property(listing => listing.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasConversion<string>();

        builder.Property(listing => listing.Type)
            .IsRequired()
            .HasMaxLength(16)
            .HasConversion<string>();

        builder.Property(listing => listing.Status)
            .IsRequired()
            .HasMaxLength(24)
            .HasConversion<string>();

        // Restrict скрізь, де знищення пов'язаного запису не повинно тягнути
        // за собою оголошення: користувача видаляють через бан, а не каскадом,
        // а місто з довідника взагалі не має зникати.
        builder.HasOne(listing => listing.Seller)
            .WithMany()
            .HasForeignKey(listing => listing.SellerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(listing => listing.City)
            .WithMany()
            .HasForeignKey(listing => listing.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Салон теж не видаляють каскадом: якщо він зникає, оголошення мають
        // лишитися й перейти під відповідальність того, хто їх подав.
        builder.HasOne(listing => listing.Dealership)
            .WithMany()
            .HasForeignKey(listing => listing.DealershipId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(listing => listing.CityDistrict)
            .WithMany()
            .HasForeignKey(listing => listing.CityDistrictId)
            .OnDelete(DeleteBehavior.Restrict);

        // Головний запит каталогу — «активні оголошення, найновіші першими».
        builder.HasIndex(listing => new { listing.Status, listing.PublishedAt });

        // Сортування за ціною йде по нормалізованій гривні, щоб різні валюти
        // порівнювалися між собою.
        builder.HasIndex(listing => new { listing.Status, listing.PriceUah });

        builder.HasIndex(listing => new { listing.SellerId, listing.Status });

        // Вітрина салону: «усі його активні оголошення».
        builder.HasIndex(listing => new { listing.DealershipId, listing.Status });
        builder.HasIndex(listing => listing.CityId);
    }
}
