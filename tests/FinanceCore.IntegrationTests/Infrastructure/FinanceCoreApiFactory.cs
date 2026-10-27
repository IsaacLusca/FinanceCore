using FinanceCore.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace FinanceCore.IntegrationTests.Infrastructure;

public sealed class FinanceCoreApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string SigningKey = "integration-tests-signing-key-with-at-least-32-bytes";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("financecore_tests")
        .WithUsername("financecore")
        .WithPassword("financecore_tests")
        .Build();

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _database.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Issuer", "FinanceCore.IntegrationTests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "FinanceCore.IntegrationTests");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", SigningKey);
        Environment.SetEnvironmentVariable("Jwt__ExpirationMinutes", "60");

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceCoreDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task ResetDatabaseAsync(CancellationToken ct = default)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceCoreDbContext>();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE users CASCADE", ct);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _database.DisposeAsync();
        await base.DisposeAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", null);
        Environment.SetEnvironmentVariable("Jwt__Issuer", null);
        Environment.SetEnvironmentVariable("Jwt__Audience", null);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", null);
        Environment.SetEnvironmentVariable("Jwt__ExpirationMinutes", null);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
