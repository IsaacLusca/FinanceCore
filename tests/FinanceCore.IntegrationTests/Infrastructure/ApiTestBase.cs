using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinanceCore.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceCore.IntegrationTests.Infrastructure;

public abstract class ApiTestBase : IAsyncLifetime
{
    protected const string Password = "SenhaForte123!";

    protected ApiTestBase(FinanceCoreApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    protected FinanceCoreApiFactory Factory { get; }

    protected HttpClient Client { get; }

    public virtual Task InitializeAsync() => Factory.ResetDatabaseAsync();

    public virtual Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    protected async Task<AuthResponse> RegisterAsync(string? email = null)
    {
        var response = await PostJsonAsync(
            "/api/auth/register",
            new { name = "Test User", email = email ?? NewEmail(), password = Password });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth!;
    }

    protected async Task<HttpResponseMessage> PostJsonAsync(
        string path,
        object body,
        string? token = null,
        string? idempotencyKey = null)
    {
        using var request = BuildRequest(HttpMethod.Post, path, token, body, idempotencyKey);
        return await Client.SendAsync(request);
    }

    protected async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token = null)
    {
        using var request = BuildRequest(method, path, token);
        return await Client.SendAsync(request);
    }

    protected static HttpRequestMessage BuildRequest(
        HttpMethod method,
        string path,
        string? token = null,
        object? body = null,
        string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(method, path);

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    protected async Task<AccountResponse> CreateAccountAsync(string token)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/accounts", token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var account = await response.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.NotNull(account);
        return account!;
    }

    protected async Task<AccountResponse> GetAccountAsync(string token, Guid accountId)
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/accounts/{accountId}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var account = await response.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.NotNull(account);
        return account!;
    }

    protected Task<HttpResponseMessage> DepositAsync(
        string token,
        Guid accountId,
        decimal amount,
        string? idempotencyKey = null) =>
        PostJsonAsync(
            $"/api/accounts/{accountId}/deposits",
            new { amount },
            token,
            idempotencyKey ?? $"dep-{Guid.NewGuid():N}");

    protected Task<HttpResponseMessage> TransferAsync(
        string token,
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string? idempotencyKey = null) =>
        PostJsonAsync(
            "/api/transfers",
            new { sourceAccountId, destinationAccountId, amount },
            token,
            idempotencyKey ?? $"trf-{Guid.NewGuid():N}");

    protected async Task<List<MovementResponse>> GetMovementsAsync(string token, Guid accountId)
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/accounts/{accountId}/movements", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var movements = await response.Content.ReadFromJsonAsync<List<MovementResponse>>();
        Assert.NotNull(movements);
        return movements!;
    }

    protected async Task<HttpResponseMessage[]> SendConcurrentlyAsync(IEnumerable<HttpRequestMessage> requests)
    {
        var materialized = requests.ToArray();

        try
        {
            return await Task.WhenAll(materialized.Select(request => Client.SendAsync(request)));
        }
        finally
        {
            foreach (var request in materialized)
            {
                request.Dispose();
            }
        }
    }

    protected Task<TResult> QueryDbAsync<TResult>(Func<FinanceCoreDbContext, Task<TResult>> query)
    {
        return QueryDbAsync(async (db, _) => await query(db));
    }

    protected async Task<TResult> QueryDbAsync<TResult>(
        Func<FinanceCoreDbContext, CancellationToken, Task<TResult>> query)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceCoreDbContext>();
        return await query(db, CancellationToken.None);
    }

    protected Task<Guid> GetUserIdByEmailAsync(string email) =>
        QueryDbAsync(db => db.Users
            .Where(user => user.Email == email)
            .Select(user => user.Id)
            .SingleAsync());

    protected Task<int> CountOperationsAsync(Guid userId) =>
        QueryDbAsync(db => db.Operations.CountAsync(operation => operation.InitiatedByUserId == userId));

    protected Task<int> CountMovementsAsync(Guid accountId) =>
        QueryDbAsync(db => db.Movements.CountAsync(movement => movement.AccountId == accountId));

    protected Task<decimal> GetLedgerSumAsync(Guid accountId) =>
        QueryDbAsync(db => db.Movements
            .Where(movement => movement.AccountId == accountId)
            .SumAsync(movement => movement.Amount));

    protected Task<string> GetPasswordHashAsync(string email) =>
        QueryDbAsync(db => db.Users
            .Where(user => user.Email == email)
            .Select(user => user.PasswordHash)
            .SingleAsync());

    protected static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    protected static void DisposeResponses(IEnumerable<HttpResponseMessage> responses)
    {
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }
}
