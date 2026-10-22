using FinanceCore.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceCore.Api.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id).HasName("pk_users");

        builder.Property(user => user.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(user => user.Name)
            .HasColumnName("name")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(user => user.Email)
            .HasColumnName("email")
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired();

        builder.Property(user => user.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(user => user.Email)
            .IsUnique()
            .HasDatabaseName("ux_users_email");
    }
}
