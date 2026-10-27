using System.Net;
using System.Net.Http.Json;
using FinanceCore.IntegrationTests.Infrastructure;

namespace FinanceCore.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class DepositTests(FinanceCoreApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task RepeatedDepositWithSameKeyIsNotDuplicated()
    {
        var alice = await RegisterAsync();
        var account = await CreateAccountAsync(alice.AccessToken);

        const string key = "deposit-replay";
        var first = await DepositAsync(alice.AccessToken, account.Id, 100m, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstOperation = await first.Content.ReadFromJsonAsync<OperationResponse>();

        var replay = await DepositAsync(alice.AccessToken, account.Id, 100m, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayOperation = await replay.Content.ReadFromJsonAsync<OperationResponse>();

        Assert.NotNull(firstOperation);
        Assert.NotNull(replayOperation);
        Assert.Equal(firstOperation!.Id, replayOperation!.Id);
        Assert.Equal(100m, (await GetAccountAsync(alice.AccessToken, account.Id)).Balance);

        var movements = await GetMovementsAsync(alice.AccessToken, account.Id);
        Assert.Single(movements);

        var mismatched = await DepositAsync(alice.AccessToken, account.Id, 50m, key);
        Assert.Equal(HttpStatusCode.Conflict, mismatched.StatusCode);
        Assert.Equal(100m, (await GetAccountAsync(alice.AccessToken, account.Id)).Balance);
        Assert.Single(await GetMovementsAsync(alice.AccessToken, account.Id));
    }

    [Fact]
    public async Task DepositWithoutIdempotencyKeyIsRejected()
    {
        var alice = await RegisterAsync();
        var account = await CreateAccountAsync(alice.AccessToken);

        var response = await PostJsonAsync(
            $"/api/accounts/{account.Id}/deposits",
            new { amount = 10m },
            alice.AccessToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0m, (await GetAccountAsync(alice.AccessToken, account.Id)).Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.001)]
    [InlineData(1.999)]
    public async Task InvalidAmountIsRejected(double amount)
    {
        var alice = await RegisterAsync();
        var account = await CreateAccountAsync(alice.AccessToken);

        var response = await DepositAsync(alice.AccessToken, account.Id, (decimal)amount, $"invalid-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0m, (await GetAccountAsync(alice.AccessToken, account.Id)).Balance);
        Assert.Empty(await GetMovementsAsync(alice.AccessToken, account.Id));
    }
}
