using FinanceCore.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceCore.Api.Data.Configurations;

public sealed class OperationConfiguration : IEntityTypeConfiguration<Operation>
{
    private const string StructureCheck =
        "(type = 'Deposit' AND source_account_id IS NULL AND destination_account_id IS NOT NULL) OR " +
        "(type = 'Transfer' AND source_account_id IS NOT NULL AND destination_account_id IS NOT NULL " +
        "AND source_account_id <> destination_account_id)";

    public void Configure(EntityTypeBuilder<Operation> builder)
    {
        builder.ToTable("operations", table =>
        {
            table.HasCheckConstraint("ck_operations_amount_positive", "amount > 0");
            table.HasCheckConstraint("ck_operations_structure", StructureCheck);
        });

        builder.HasKey(operation => operation.Id).HasName("pk_operations");

        builder.Property(operation => operation.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(operation => operation.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(operation => operation.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(operation => operation.SourceAccountId)
            .HasColumnName("source_account_id");

        builder.Property(operation => operation.DestinationAccountId)
            .HasColumnName("destination_account_id");

        builder.Property(operation => operation.InitiatedByUserId)
            .HasColumnName("initiated_by_user_id")
            .IsRequired();

        builder.Property(operation => operation.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(operation => operation.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasOne(operation => operation.SourceAccount)
            .WithMany()
            .HasForeignKey(operation => operation.SourceAccountId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_operations_source_account");

        builder.HasOne(operation => operation.DestinationAccount)
            .WithMany()
            .HasForeignKey(operation => operation.DestinationAccountId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_operations_destination_account");

        builder.HasOne(operation => operation.InitiatedByUser)
            .WithMany()
            .HasForeignKey(operation => operation.InitiatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_operations_users");

        builder.HasIndex(operation => new { operation.InitiatedByUserId, operation.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("ux_operations_user_idempotency_key");

        builder.HasIndex(operation => operation.SourceAccountId)
            .HasDatabaseName("ix_operations_source_account_id");

        builder.HasIndex(operation => operation.DestinationAccountId)
            .HasDatabaseName("ix_operations_destination_account_id");
    }
}
