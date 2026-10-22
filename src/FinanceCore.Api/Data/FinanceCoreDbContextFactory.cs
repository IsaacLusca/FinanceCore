using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FinanceCore.Api.Data;

public sealed class FinanceCoreDbContextFactory : IDesignTimeDbContextFactory<FinanceCoreDbContext>
{
    public FinanceCoreDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<FinanceCoreDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'Default' não configurada. Defina ConnectionStrings__Default " +
                "ou use 'dotnet user-secrets' antes de executar os comandos do Entity Framework.");
        }

        var options = new DbContextOptionsBuilder<FinanceCoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new FinanceCoreDbContext(options);
    }
}
