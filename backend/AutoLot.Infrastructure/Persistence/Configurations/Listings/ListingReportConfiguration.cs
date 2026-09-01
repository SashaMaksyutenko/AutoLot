using AutoLot.Domain.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Listings;

internal sealed class ListingReportConfiguration : IEntityTypeConfiguration<ListingReport>
{
    public void Configure(EntityTypeBuilder<ListingReport> builder)
    {
        builder.ToTable("listing_reports");

        builder.Property(report => report.Comment).HasMaxLength(1000);

        builder.Property(report => report.ReviewNote).HasMaxLength(1000);

        builder.HasOne(report => report.Listing)
            .WithMany()
            .HasForeignKey(report => report.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Скаржника не стирають каскадом: людину можуть заблокувати, а
        // скарга лишається — саме за нею оголошення й зняли.
        builder.HasOne(report => report.Reporter)
            .WithMany()
            .HasForeignKey(report => report.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(report => report.ReviewedBy)
            .WithMany()
            .HasForeignKey(report => report.ReviewedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Черга модератора: «нерозглянуті, найдавніші зверху».
        builder.HasIndex(report => new { report.Status, report.CreatedAt });

        // Перевірка «чи ця людина вже скаржилася на цей лот» і підрахунок
        // скарг на одне оголошення — обидва йдуть по цій парі.
        builder.HasIndex(report => new { report.ListingId, report.ReporterId });
    }
}
