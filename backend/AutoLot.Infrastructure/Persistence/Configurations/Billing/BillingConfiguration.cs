using AutoLot.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoLot.Infrastructure.Persistence.Configurations.Billing;

internal sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("wallets");

        // Один гаманець на людину. Саме індексом: без нього паралельні
        // запити могли б створити два, і баланс роздвоївся б.
        builder.HasIndex(wallet => wallet.UserId).IsUnique();

        builder.HasOne(wallet => wallet.User)
            .WithMany()
            .HasForeignKey(wallet => wallet.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("wallet_transactions");

        builder.HasOne(item => item.Wallet)
            .WithMany(wallet => wallet.Transactions)
            .HasForeignKey(item => item.WalletId)
            .OnDelete(DeleteBehavior.Cascade);

        // Головний запит історії: «мої рухи, найсвіжіші зверху».
        builder.HasIndex(item => new { item.WalletId, item.CreatedAt });
    }
}

internal sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("plans");

        builder.Property(plan => plan.Code).IsRequired().HasMaxLength(32);

        // Код — ключ сіду: за ним повторний запуск знаходить наявний план,
        // а не створює другий такий самий.
        builder.HasIndex(plan => plan.Code).IsUnique();
    }
}

internal sealed class PlanTranslationConfiguration : IEntityTypeConfiguration<PlanTranslation>
{
    public void Configure(EntityTypeBuilder<PlanTranslation> builder)
    {
        builder.ToTable("plan_translations");

        builder.Property(item => item.Name).IsRequired().HasMaxLength(64);
        builder.Property(item => item.Description).HasMaxLength(256);
        builder.Property(item => item.Language).IsRequired().HasMaxLength(8);

        builder.HasOne(item => item.Plan)
            .WithMany(plan => plan.Translations)
            .HasForeignKey(item => item.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.PlanId, item.Language }).IsUnique();
    }
}

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions");

        builder.HasOne(item => item.User)
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // План не стирають каскадом: тариф можуть прибрати з довідника, а
        // оплачені за ним періоди мають лишитися разом із їхньою ціною.
        builder.HasOne(item => item.Plan)
            .WithMany()
            .HasForeignKey(item => item.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        // Головне питання до цієї таблиці: «що діє в цієї людини зараз».
        builder.HasIndex(item => new { item.UserId, item.EndsAt });
    }
}
