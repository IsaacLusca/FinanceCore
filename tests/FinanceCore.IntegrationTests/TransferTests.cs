using System.Net;
using System.Net.Http.Json;
using FinanceCore.Api.Common;
using FinanceCore.Api.Domain;
using FinanceCore.IntegrationTests.Infrastructure;

namespace FinanceCore.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class TransferTests(FinanceCoreApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task ValidTransferMovesFundsInBothAccounts()
    {
        var alice = await RegisterAsync();
        var bob = await RegisterAsync();
        var source = await CreateAccountAsync(alice.AccessToken);
        var destination = await CreateAccountAsync(bob.AccessToken);

        var deposit = await DepositAsync(alice.AccessToken, source.Id, 100m, "deposit-valid-transfer");
        Assert.Equal(HttpStatusCode.Created, deposit.StatusCode);

        var transfer = await TransferAsync(alice.AccessToken, source.Id, destination.Id, 30m, "transfer-valid");
        Assert.Equal(HttpStatusCode.Created, transfer.StatusCode);

        var operation = await transfer.Content.ReadFromJsonAsync<OperationResponse>();
        Assert.NotNull(operation);
        Assert.Equal(70m, operation!.SourceBalanceAfter!.Value);
        Assert.Equal(30m, operation.DestinationBalanceAfter!.Value);

        Assert.Equal(70m, (await GetAccountAsync(alice.AccessToken, source.Id)).Balance);
        Assert.Equal(30m, (await GetAccountAsync(bob.AccessToken, destination.Id)).Balance);

        var sourceMovements = await GetMovementsAsync(alice.AccessToken, source.Id);
        var destinationMovements = await GetMovementsAsync(bob.AccessToken, destination.Id);

        Assert.Equal(2, sourceMovements.Count);
        var debit = Assert.Single(sourceMovements, movement => movement.Type == OperationType.Transfer);
        Assert.Equal(-30m, debit.Amount);
        Assert.Equal(70m, debit.BalanceAfter);
        Assert.Equal(destination.Id, debit.CounterpartyAccountId);

        var credit = Assert.Single(destinationMovements);
        Assert.Equal(30m, credit.Amount);
        Assert.Equal(30m, credit.BalanceAfter);
        Assert.Equal(source.Id, credit.CounterpartyAccountId);
    }

    [Fact]
    public async Task InsufficientFundsDoesNotChangeEitherAccount()
    {
        var alice = await RegisterAsync();
        var bob = await RegisterAsync();
        var source = await CreateAccountAsync(alice.AccessToken);
        var destination = await CreateAccountAsync(bob.AccessToken);

        var deposit = await DepositAsync(alice.AccessToken, source.Id, 50m, "deposit-insufficient");
        Assert.Equal(HttpStatusCode.Created, deposit.StatusCode);

        var transfer = await TransferAsync(alice.AccessToken, source.Id, destination.Id, 51m, "transfer-insufficient");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, transfer.StatusCode);

        Assert.Equal(50m, (await GetAccountAsync(alice.AccessToken, source.Id)).Balance);
        Assert.Equal(0m, (await GetAccountAsync(bob.AccessToken, destination.Id)).Balance);

        Assert.Single(await GetMovementsAsync(alice.AccessToken, source.Id));
        Assert.Empty(await GetMovementsAsync(bob.AccessToken, destination.Id));
    }

    [Fact]
    public async Task CreditFailureDoesNotApplyDebit()
    {
        var alice = await RegisterAsync();
        var bob = await RegisterAsync();
        var source = await CreateAccountAsync(alice.AccessToken);
        var destination = await CreateAccountAsync(bob.AccessToken);

        var maxDeposit = await DepositAsync(bob.AccessToken, destination.Id, MoneyRules.MaxAmount, "deposit-max");
        Assert.Equal(HttpStatusCode.Created, maxDeposit.StatusCode);

        var sourceDeposit = await DepositAsync(alice.AccessToken, source.Id, 10m, "deposit-source");
        Assert.Equal(HttpStatusCode.Created, sourceDeposit.StatusCode);

        var transfer = await TransferAsync(alice.AccessToken, source.Id, destination.Id, 1m, "transfer-credit-failure");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, transfer.StatusCode);

        Assert.Equal(10m, (await GetAccountAsync(alice.AccessToken, source.Id)).Balance);
        Assert.Equal(MoneyRules.MaxAmount, (await GetAccountAsync(bob.AccessToken, destination.Id)).Balance);
        Assert.Single(await GetMovementsAsync(alice.AccessToken, source.Id));
    }

    [Fact]
    public async Task RepeatedTransferWithSameKeyIsNotDuplicated()
    {
        var alice = await RegisterAsync();
        var bob = await RegisterAsync();
        var source = await CreateAccountAsync(alice.AccessToken);
        var destination = await CreateAccountAsync(bob.AccessToken);

        Assert.Equal(
            HttpStatusCode.Created,
            (await DepositAsync(alice.AccessToken, source.Id, 100m, "deposit-replay")).StatusCode);

        const string key = "transfer-replay";
        var first = await TransferAsync(alice.AccessToken, source.Id, destination.Id, 30m, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstOperation = await first.Content.ReadFromJsonAsync<OperationResponse>();

        var replay = await TransferAsync(alice.AccessToken, source.Id, destination.Id, 30m, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayOperation = await replay.Content.ReadFromJsonAsync<OperationResponse>();

        Assert.NotNull(firstOperation);
        Assert.NotNull(replayOperation);
        Assert.Equal(firstOperation!.Id, replayOperation!.Id);
        Assert.Equal(70m, (await GetAccountAsync(alice.AccessToken, source.Id)).Balance);
        Assert.Equal(30m, (await GetAccountAsync(bob.AccessToken, destination.Id)).Balance);
        Assert.Equal(2, (await GetMovementsAsync(alice.AccessToken, source.Id)).Count);
        Assert.Single(await GetMovementsAsync(bob.AccessToken, destination.Id));

        var mismatched = await TransferAsync(alice.AccessToken, source.Id, destination.Id, 31m, key);
        Assert.Equal(HttpStatusCode.Conflict, mismatched.StatusCode);
        Assert.Equal(70m, (await GetAccountAsync(alice.AccessToken, source.Id)).Balance);
    }

    [Fact]
    public async Task ConcurrentTransfersDoNotProduceNegativeBalance()
    {
        var alice = await RegisterAsync();
        var bob = await RegisterAsync();
        var source = await CreateAccountAsync(alice.AccessToken);
        var destination = await CreateAccountAsync(bob.AccessToken);

        Assert.Equal(
            HttpStatusCode.Created,
            (await DepositAsync(alice.AccessToken, source.Id, 100m, "deposit-race")).StatusCode);

        var requestOne = BuildRequest(
            HttpMethod.Post,
            "/api/transfers",
            alice.AccessToken,
            new { sourceAccountId = source.Id, destinationAccountId = destination.Id, amount = 80m },
            "race-one");

        var requestTwo = BuildRequest(
            HttpMethod.Post,
            "/api/transfers",
            alice.AccessToken,
            new { sourceAccountId = source.Id, destinationAccountId = destination.Id, amount = 80m },
            "race-two");

        var responses = await Task.WhenAll(Client.SendAsync(requestOne), Client.SendAsync(requestTwo));

        try
        {
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
            Assert.Equal(
                1,
                responses.Count(response =>
                    response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.Conflict));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }

            requestOne.Dispose();
            requestTwo.Dispose();
        }

        var sourceAccount = await GetAccountAsync(alice.AccessToken, source.Id);
        var destinationAccount = await GetAccountAsync(bob.AccessToken, destination.Id);

        Assert.Equal(20m, sourceAccount.Balance);
        Assert.Equal(80m, destinationAccount.Balance);
        Assert.True(sourceAccount.Balance >= 0);
        Assert.True(destinationAccount.Balance >= 0);

        var sourceMovements = await GetMovementsAsync(alice.AccessToken, source.Id);
        var destinationMovements = await GetMovementsAsync(bob.AccessToken, destination.Id);

        Assert.Equal(2, sourceMovements.Count);
        Assert.Single(destinationMovements);
        Assert.Equal(sourceAccount.Balance, sourceMovements.Sum(movement => movement.Amount));
        Assert.Equal(destinationAccount.Balance, destinationMovements.Sum(movement => movement.Amount));
    }
}
