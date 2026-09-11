using AutoLot.Domain.Bots;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Bots;

internal sealed class BotLinkConfiguration : IEntityTypeConfiguration<BotLink>
{
    public void Configure(EntityTypeBuilder<BotLink> builder)
    {
        builder.ToTable("bot_links");

        builder.Property(link => link.ChatId).IsRequired().HasMaxLength(64);
        builder.Property(link => link.ChatName).HasMaxLength(128);

        // Перелічення текстом, як і решта в цьому проєкті: у дампі бази має
        // читатися «Telegram», а не «0».
        builder.Property(link => link.Provider).HasConversion<string>().HasMaxLength(16);

        builder.HasOne(link => link.User)
            .WithMany()
            .HasForeignKey(link => link.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Один чат — один господар. Без цього обмеження та сама розмова могла б
        // опинитися прив'язаною до двох акаунтів, і бот не знав би, кому
        // надсилати сповіщення.
        builder.HasIndex(link => new { link.Provider, link.ChatId }).IsUnique();

        // Зворотний бік: «куди писати цій людині» — головний запит сповіщень.
        builder.HasIndex(link => link.UserId);
    }
}

internal sealed class BotLinkCodeConfiguration : IEntityTypeConfiguration<BotLinkCode>
{
    public void Configure(EntityTypeBuilder<BotLinkCode> builder)
    {
        builder.ToTable("bot_link_codes");

        builder.Property(code => code.Code).IsRequired().HasMaxLength(BotLinkCode.Length);

        builder.HasOne(code => code.User)
            .WithMany()
            .HasForeignKey(code => code.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Бот шукає рівно за кодом і більше ні за чим. Унікальність тут не
        // ставимо: код живе десять хвилин, а рядки лишаються назавжди, і два
        // однакові набори з різницею в місяць — нормальна річ.
        builder.HasIndex(code => code.Code);
    }
}
