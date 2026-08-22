using AutoLot.Domain.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Listings;

internal sealed class CarConfiguration : IEntityTypeConfiguration<Car>
{
    public void Configure(EntityTypeBuilder<Car> builder)
    {
        builder.ToTable("cars");

        // Одне авто на оголошення: унікальний FK і робить зв'язок «один до одного».
        builder.HasOne(car => car.Listing)
            .WithOne(listing => listing.Car)
            .HasForeignKey<Car>(car => car.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(car => car.Vin)
            .HasMaxLength(17);

        // Перелічення зберігаємо рядками — база лишається читабельною.
        builder.Property(car => car.Condition).IsRequired().HasMaxLength(16).HasConversion<string>();
        builder.Property(car => car.FuelType).IsRequired().HasMaxLength(24).HasConversion<string>();
        builder.Property(car => car.Transmission).IsRequired().HasMaxLength(16).HasConversion<string>();
        builder.Property(car => car.Drivetrain).IsRequired().HasMaxLength(16).HasConversion<string>();
        builder.Property(car => car.BodyType).IsRequired().HasMaxLength(16).HasConversion<string>();
        builder.Property(car => car.Color).IsRequired().HasMaxLength(16).HasConversion<string>();
        builder.Property(car => car.DamageState).IsRequired().HasMaxLength(16).HasConversion<string>();
        builder.Property(car => car.PaintCondition).HasMaxLength(24).HasConversion<string>();
        builder.Property(car => car.ChargingPort).HasMaxLength(16).HasConversion<string>();
        builder.Property(car => car.EcologyStandard).HasMaxLength(8).HasConversion<string>();

        // Об'єм двигуна й витрата палива — з десятими: 1.6 л, 7.5 л/100 км.
        // Глобальна умовність decimal(18,2) тут завелика, звужуємо.
        builder.Property(car => car.EngineVolume).HasPrecision(4, 1);
        builder.Property(car => car.FuelConsumptionCity).HasPrecision(4, 1);
        builder.Property(car => car.FuelConsumptionHighway).HasPrecision(4, 1);
        builder.Property(car => car.FuelConsumptionCombined).HasPrecision(4, 1);
        builder.Property(car => car.BatteryCapacity).HasPrecision(6, 2);

        builder.Property(car => car.IsCustomsCleared).HasDefaultValue(true);
        builder.Property(car => car.IsLocatedInUkraine).HasDefaultValue(true);

        builder.HasOne(car => car.Make)
            .WithMany()
            .HasForeignKey(car => car.MakeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(car => car.Model)
            .WithMany()
            .HasForeignKey(car => car.ModelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(car => car.Generation)
            .WithMany()
            .HasForeignKey(car => car.GenerationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(car => car.ManufacturerCountry)
            .WithMany()
            .HasForeignKey(car => car.ManufacturerCountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(car => car.ImportedFromCountry)
            .WithMany()
            .HasForeignKey(car => car.ImportedFromCountryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Найчастіші зв'язки фільтрів каталогу.
        builder.HasIndex(car => new { car.MakeId, car.ModelId });
        builder.HasIndex(car => car.Year);
        builder.HasIndex(car => car.Mileage);
        builder.HasIndex(car => car.FuelType);
        builder.HasIndex(car => car.BodyType);
    }
}

internal sealed class CarFeatureConfiguration : IEntityTypeConfiguration<CarFeature>
{
    public void Configure(EntityTypeBuilder<CarFeature> builder)
    {
        builder.ToTable("car_features");

        // Складений ключ: одна опція може бути в авто лише раз.
        builder.HasKey(link => new { link.CarId, link.FeatureId });

        builder.HasOne(link => link.Car)
            .WithMany(car => car.Features)
            .HasForeignKey(link => link.CarId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(link => link.Feature)
            .WithMany()
            .HasForeignKey(link => link.FeatureId)
            .OnDelete(DeleteBehavior.Restrict);

        // Зворотний пошук: «усі авто з підігрівом сидінь».
        builder.HasIndex(link => link.FeatureId);
    }
}

internal sealed class CarPhotoConfiguration : IEntityTypeConfiguration<CarPhoto>
{
    public void Configure(EntityTypeBuilder<CarPhoto> builder)
    {
        builder.ToTable("car_photos");

        builder.Property(photo => photo.Path)
            .IsRequired()
            .HasMaxLength(300);

        builder.HasOne(photo => photo.Car)
            .WithMany(car => car.Photos)
            .HasForeignKey(photo => photo.CarId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(photo => new { photo.CarId, photo.SortOrder });
    }
}
