using AutoLot.Domain.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Search;

internal sealed class SavedSearchConfiguration : IEntityTypeConfiguration<SavedSearch>
{
    public void Configure(EntityTypeBuilder<SavedSearch> builder)
    {
        builder.ToTable("saved_searches");

        builder.Property(search => search.Name)
            .IsRequired()
            .HasMaxLength(SavedSearch.MaxNameLength);

        // Довжину JSON не обмежуємо: фільтрів у каталозі три десятки, і
        // штучна межа впала б рівно тоді, коли хтось увімкне їх усі.
        builder.Property(search => search.QueryJson).IsRequired();

        builder.HasOne(search => search.User)
            .WithMany()
            .HasForeignKey(search => search.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Єдиний запит до цієї таблиці: «мої пошуки, найновіші зверху».
        builder.HasIndex(search => new { search.UserId, search.CreatedAt });
    }
}
