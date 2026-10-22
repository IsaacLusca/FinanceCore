using FinanceCore.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceCore.Api.Data.Configurations;

public sealed class MovementConfiguration : IEntityTypeConfiguration<Movement>
{
    public void Configure(EntityTypeBuilder<Movement> builder)
    {
        builder.ToTable("movements", table =>
        {
            table.HasCheckConstraint("ck_movements_amount_non_zero", "amount <> 0");
            table.HasCheckConstraint("ck_movements_balance_after_non_negative", "balance_after >= 0");
        });

        builder.HasKey(movement => movement.Id).HasName("pk_movements");

        builder.Property(movement => movement.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(movement => movement.OperationId)
            .HasColumnName("operation_id")
            .IsRequired();

        builder.Property(movement => movement.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(movement => movement.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(movement => movement.BalanceAfter)
            .HasColumnName("balance_after")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(movement => movement.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasOne(movement => movement.Operation)
            .WithMany(operation => operation.Movements)
            .HasForeignKey(movement => movement.OperationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_movements_operations");

        builder.HasOne(movement => movement.Account)
            .WithMany()
            .HasForeignKey(movement => movement.AccountId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_movements_accounts");

        builder.HasIndex(movement => new { movement.OperationId, movement.AccountId })
            .IsUnique()
            .HasDatabaseName("ux_movements_operation_account");

        builder.HasIndex(movement => new { movement.AccountId, movement.CreatedAtUtc })
            .HasDatabaseName("ix_movements_account_created_at");
    }
}
