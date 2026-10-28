using System.Net;
using System.Net.Http.Json;
using FinanceCore.IntegrationTests.Infrastructure;

namespace FinanceCore.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class IdempotencyTests(FinanceCoreApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task ConcurrentDuplicateDepositsCreateSingleOperation()
    {
        const int attempts = 5;
        const string key = "concurrent-deposit-key";

        var aliceEmail = NewEmail();
        var alice = await RegisterAsync(aliceEmail);
        var account = await CreateAccountAsync(alice.AccessToken);

        var requests = Enumerable.Range(0, attempts)
            .Select(_ => BuildRequest(
                HttpMethod.Post,
                $"/api/accounts/{account.Id}/deposits",
                alice.AccessToken,
                new { amount = 50m },
                key))
            .ToArray();

        var responses = await SendConcurrentlyAsync(requests);

        try
        {
            var statuses = responses.Select(response => response.StatusCode).ToArray();

            Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.Created));
            Assert.All(
                statuses,
                status => Assert.True(
                    status is HttpStatusCode.Created or HttpStatusCode.OK,
                    $"Status inesperado: {status}"));

            var operations = new List<OperationResponse>();
            foreach (var response in responses)
            {
                var operation = await response.Content.ReadFromJsonAsync<OperationResponse>();
                Assert.NotNull(operation);
                operations.Add(operation!);
            }

            Assert.Single(operations.Select(operation => operation.Id).Distinct());

            var finalAccount = await GetAccountAsync(alice.AccessToken, account.Id);
            Assert.Equal(50m, finalAccount.Balance);
            Assert.Equal(50m, await GetLedgerSumAsync(account.Id));
            Assert.Equal(1, await CountOperationsAsync(await GetUserIdByEmailAsync(aliceEmail)));
            Assert.Equal(1, await CountMovementsAsync(account.Id));
        }
        finally
        {
            DisposeResponses(responses);
        }
    }

    [Fact]
    public async Task ConcurrentDuplicateTransfersCreateSingleOperation()
    {
        const int attempts = 4;
        const string key = "concurrent-transfer-key";

        var aliceEmail = NewEmail();
        var alice = await RegisterAsync(aliceEmail);
        var bob = await RegisterAsync();
        var source = await CreateAccountAsync(alice.AccessToken);
        var destination = await CreateAccountAsync(bob.AccessToken);

        Assert.Equal(
            HttpStatusCode.Created,
            (await DepositAsync(alice.AccessToken, source.Id, 100m, "concurrent-transfer-deposit")).StatusCode);

        var requests = Enumerable.Range(0, attempts)
            .Select(_ => BuildRequest(
                HttpMethod.Post,
                "/api/transfers",
                alice.AccessToken,
                new { sourceAccountId = source.Id, destinationAccountId = destination.Id, amount = 30m },
                key))
            .ToArray();

        var responses = await SendConcurrentlyAsync(requests);

        try
        {
            var statuses = responses.Select(response => response.StatusCode).ToArray();

            Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.Created));
            Assert.All(
                statuses,
                status => Assert.True(
                    status is HttpStatusCode.Created or HttpStatusCode.OK,
                    $"Status inesperado: {status}"));

            var operations = new List<OperationResponse>();
            foreach (var response in responses)
            {
                var operation = await response.Content.ReadFromJsonAsync<OperationResponse>();
                Assert.NotNull(operation);
                operations.Add(operation!);
            }

            Assert.Single(operations.Select(operation => operation.Id).Distinct());

            Assert.Equal(70m, (await GetAccountAsync(alice.AccessToken, source.Id)).Balance);
            Assert.Equal(30m, (await GetAccountAsync(bob.AccessToken, destination.Id)).Balance);
            Assert.Equal(2, await CountOperationsAsync(await GetUserIdByEmailAsync(aliceEmail)));
            Assert.Equal(2, await CountMovementsAsync(source.Id));
            Assert.Equal(1, await CountMovementsAsync(destination.Id));
        }
        finally
        {
            DisposeResponses(responses);
        }
    }

    [Fact]
    public async Task SameKeyIsScopedPerUser()
    {
        const string sharedKey = "shared-idempotency-key";

        var alice = await RegisterAsync();
        var bob = await RegisterAsync();
        var aliceAccount = await CreateAccountAsync(alice.AccessToken);
        var bobAccount = await CreateAccountAsync(bob.AccessToken);

        var requests = new[]
        {
            BuildRequest(
                HttpMethod.Post,
                $"/api/accounts/{aliceAccount.Id}/deposits",
                alice.AccessToken,
                new { amount = 10m },
                sharedKey),
            BuildRequest(
                HttpMethod.Post,
                $"/api/accounts/{bobAccount.Id}/deposits",
                bob.AccessToken,
                new { amount = 10m },
                sharedKey)
        };

        var responses = await SendConcurrentlyAsync(requests);

        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));

            var operations = new List<OperationResponse>();
            foreach (var response in responses)
            {
                var operation = await response.Content.ReadFromJsonAsync<OperationResponse>();
                Assert.NotNull(operation);
                operations.Add(operation!);
            }

            Assert.NotEqual(operations[0].Id, operations[1].Id);
            Assert.Equal(10m, (await GetAccountAsync(alice.AccessToken, aliceAccount.Id)).Balance);
            Assert.Equal(10m, (await GetAccountAsync(bob.AccessToken, bobAccount.Id)).Balance);
        }
        finally
        {
            DisposeResponses(responses);
        }
    }

    [Fact]
    public async Task SameKeyForDifferentOperationTypeConflicts()
    {
        const string key = "deposit-then-transfer";

        var aliceEmail = NewEmail();
        var alice = await RegisterAsync(aliceEmail);
        var bob = await RegisterAsync();
        var source = await CreateAccountAsync(alice.AccessToken);
        var destination = await CreateAccountAsync(bob.AccessToken);

        Assert.Equal(
            HttpStatusCode.Created,
            (await DepositAsync(alice.AccessToken, source.Id, 10m, key)).StatusCode);

        var transfer = await TransferAsync(alice.AccessToken, source.Id, destination.Id, 5m, key);

        Assert.Equal(HttpStatusCode.Conflict, transfer.StatusCode);
        Assert.Equal(10m, (await GetAccountAsync(alice.AccessToken, source.Id)).Balance);
        Assert.Equal(0m, (await GetAccountAsync(bob.AccessToken, destination.Id)).Balance);
        Assert.Equal(1, await CountOperationsAsync(await GetUserIdByEmailAsync(aliceEmail)));
        Assert.Equal(1, await CountMovementsAsync(source.Id));
    }
}
