using AutoLot.Domain.Cars;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Cars;

internal sealed class MakeConfiguration : IEntityTypeConfiguration<Make>
{
    public void Configure(EntityTypeBuilder<Make> builder)
    {
        builder.ToTable("makes");

        builder.Property(make => make.Name).IsRequired().HasMaxLength(64);
        builder.Property(make => make.Slug).IsRequired().HasMaxLength(64);

        builder.HasIndex(make => make.Slug).IsUnique();

        // Списки марок завжди йдуть «спершу популярні, далі за абеткою».
        builder.HasIndex(make => new { make.IsPopular, make.Name });
    }
}

internal sealed class ModelConfiguration : IEntityTypeConfiguration<Model>
{
    public void Configure(EntityTypeBuilder<Model> builder)
    {
        builder.ToTable("models");

        builder.Property(model => model.Name).IsRequired().HasMaxLength(64);
        builder.Property(model => model.Slug).IsRequired().HasMaxLength(96);

        builder.HasOne(model => model.Make)
            .WithMany(make => make.Models)
            .HasForeignKey(model => model.MakeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(model => model.Slug).IsUnique();
        builder.HasIndex(model => new { model.MakeId, model.Name });
    }
}

internal sealed class GenerationConfiguration : IEntityTypeConfiguration<Generation>
{
    public void Configure(EntityTypeBuilder<Generation> builder)
    {
        builder.ToTable("generations");

        builder.Property(generation => generation.Name).IsRequired().HasMaxLength(64);
        builder.Property(generation => generation.Slug).IsRequired().HasMaxLength(128);

        builder.HasOne(generation => generation.Model)
            .WithMany(model => model.Generations)
            .HasForeignKey(generation => generation.ModelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(generation => generation.Slug).IsUnique();
        builder.HasIndex(generation => new { generation.ModelId, generation.YearFrom });
    }
}
