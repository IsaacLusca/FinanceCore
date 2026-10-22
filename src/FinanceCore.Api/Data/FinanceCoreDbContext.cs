using FinanceCore.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinanceCore.Api.Data;

public sealed class FinanceCoreDbContext(DbContextOptions<FinanceCoreDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Operation> Operations => Set<Operation>();

    public DbSet<Movement> Movements => Set<Movement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceCoreDbContext).Assembly);
    }
}
