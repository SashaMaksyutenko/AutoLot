using AutoLot.Domain.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Listings;

internal sealed class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> builder)
    {
        builder.ToTable("favorites");

        // Складений ключ із двох стовпців. Він не лише ідентифікує рядок, а й
        // сам собою забороняє дублікати: додати оголошення в обране двічі
        // база просто не дасть, і перевіряти це в коді додатково не треба.
        builder.HasKey(favorite => new { favorite.UserId, favorite.ListingId });

        // Разом із оголошенням зникають і всі позначки про нього — тримати
        // обране на те, чого немає, немає сенсу.
        builder.HasOne(favorite => favorite.Listing)
            .WithMany()
            .HasForeignKey(favorite => favorite.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(favorite => favorite.User)
            .WithMany()
            .HasForeignKey(favorite => favorite.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Головний запит сторінки «Обране»: усе моє, найсвіжіше зверху.
        // Перший стовпець ключа (UserId) уже покриває пошук за користувачем,
        // але без CreatedAt база сортувала б знайдене окремим кроком.
        builder.HasIndex(favorite => new { favorite.UserId, favorite.CreatedAt });
    }
}
