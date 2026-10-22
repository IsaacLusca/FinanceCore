using FinanceCore.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceCore.Api.Data.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts", table =>
            table.HasCheckConstraint("ck_accounts_balance_non_negative", "balance >= 0"));

        builder.HasKey(account => account.Id).HasName("pk_accounts");

        builder.Property(account => account.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(account => account.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(account => account.Balance)
            .HasColumnName("balance")
            .HasPrecision(18, 2)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(account => account.Version)
            .IsRowVersion();

        builder.Property(account => account.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasOne(account => account.User)
            .WithMany(user => user.Accounts)
            .HasForeignKey(account => account.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_accounts_users");

        builder.HasIndex(account => account.UserId)
            .HasDatabaseName("ix_accounts_user_id");
    }
}
