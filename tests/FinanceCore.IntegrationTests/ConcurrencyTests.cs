using System.Net;
using FinanceCore.IntegrationTests.Infrastructure;

namespace FinanceCore.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ConcurrencyTests(FinanceCoreApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task ManyConcurrentTransfersKeepBalancesAndLedgerConsistent()
    {
        const int attempts = 20;
        const decimal transferAmount = 10m;
        const decimal initialDeposit = 100m;

        var aliceEmail = NewEmail();
        var bobEmail = NewEmail();
        var alice = await RegisterAsync(aliceEmail);
        var bob = await RegisterAsync(bobEmail);
        var source = await CreateAccountAsync(alice.AccessToken);
        var destination = await CreateAccountAsync(bob.AccessToken);

        Assert.Equal(
            HttpStatusCode.Created,
            (await DepositAsync(alice.AccessToken, source.Id, initialDeposit, "many-deposit")).StatusCode);

        var requests = Enumerable.Range(0, attempts)
            .Select(index => BuildRequest(
                HttpMethod.Post,
                "/api/transfers",
                alice.AccessToken,
                new { sourceAccountId = source.Id, destinationAccountId = destination.Id, amount = transferAmount },
                $"many-{index}"))
            .ToArray();

        var responses = await SendConcurrentlyAsync(requests);

        try
        {
            var statuses = responses.Select(response => response.StatusCode).ToArray();

            Assert.All(
                statuses,
                status => Assert.True(
                    status is HttpStatusCode.Created or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity,
                    $"Status inesperado: {status}"));

            var successes = statuses.Count(status => status == HttpStatusCode.Created);
            Assert.InRange(successes, 1, (int)(initialDeposit / transferAmount));

            var sourceAccount = await GetAccountAsync(alice.AccessToken, source.Id);
            var destinationAccount = await GetAccountAsync(bob.AccessToken, destination.Id);
            var expectedSourceBalance = initialDeposit - transferAmount * successes;
            var expectedDestinationBalance = transferAmount * successes;

            Assert.Equal(expectedSourceBalance, sourceAccount.Balance);
            Assert.Equal(expectedDestinationBalance, destinationAccount.Balance);
            Assert.True(sourceAccount.Balance >= 0);
            Assert.True(destinationAccount.Balance >= 0);

            Assert.Equal(sourceAccount.Balance, await GetLedgerSumAsync(source.Id));
            Assert.Equal(destinationAccount.Balance, await GetLedgerSumAsync(destination.Id));

            Assert.Equal(1 + successes, await CountOperationsAsync(await GetUserIdByEmailAsync(aliceEmail)));
            Assert.Equal(0, await CountOperationsAsync(await GetUserIdByEmailAsync(bobEmail)));
            Assert.Equal(1 + successes, await CountMovementsAsync(source.Id));
            Assert.Equal(successes, await CountMovementsAsync(destination.Id));
        }
        finally
        {
            DisposeResponses(responses);
        }
    }

    [Fact]
    public async Task OppositeConcurrentTransfersPreserveTotalAndLedger()
    {
        var aliceEmail = NewEmail();
        var bobEmail = NewEmail();
        var alice = await RegisterAsync(aliceEmail);
        var bob = await RegisterAsync(bobEmail);
        var aliceAccount = await CreateAccountAsync(alice.AccessToken);
        var bobAccount = await CreateAccountAsync(bob.AccessToken);

        Assert.Equal(
            HttpStatusCode.Created,
            (await DepositAsync(alice.AccessToken, aliceAccount.Id, 100m, "opposite-deposit-alice")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Created,
            (await DepositAsync(bob.AccessToken, bobAccount.Id, 100m, "opposite-deposit-bob")).StatusCode);

        var requests = new[]
        {
            BuildRequest(
                HttpMethod.Post,
                "/api/transfers",
                alice.AccessToken,
                new { sourceAccountId = aliceAccount.Id, destinationAccountId = bobAccount.Id, amount = 30m },
                "opposite-alice-to-bob"),
            BuildRequest(
                HttpMethod.Post,
                "/api/transfers",
                bob.AccessToken,
                new { sourceAccountId = bobAccount.Id, destinationAccountId = aliceAccount.Id, amount = 20m },
                "opposite-bob-to-alice")
        };

        var responses = await SendConcurrentlyAsync(requests);

        try
        {
            var statuses = responses.Select(response => response.StatusCode).ToArray();

            Assert.All(
                statuses,
                status => Assert.True(
                    status is HttpStatusCode.Created or HttpStatusCode.Conflict,
                    $"Status inesperado: {status}"));

            var aliceTransferSucceeded = statuses[0] == HttpStatusCode.Created;
            var bobTransferSucceeded = statuses[1] == HttpStatusCode.Created;
            var successes = (aliceTransferSucceeded ? 1 : 0) + (bobTransferSucceeded ? 1 : 0);

            Assert.InRange(successes, 1, 2);

            var finalAlice = await GetAccountAsync(alice.AccessToken, aliceAccount.Id);
            var finalBob = await GetAccountAsync(bob.AccessToken, bobAccount.Id);

            Assert.Equal(200m, finalAlice.Balance + finalBob.Balance);
            Assert.True(finalAlice.Balance >= 0);
            Assert.True(finalBob.Balance >= 0);
            Assert.Equal(finalAlice.Balance, await GetLedgerSumAsync(aliceAccount.Id));
            Assert.Equal(finalBob.Balance, await GetLedgerSumAsync(bobAccount.Id));

            Assert.Equal(
                1 + (aliceTransferSucceeded ? 1 : 0),
                await CountOperationsAsync(await GetUserIdByEmailAsync(aliceEmail)));
            Assert.Equal(
                1 + (bobTransferSucceeded ? 1 : 0),
                await CountOperationsAsync(await GetUserIdByEmailAsync(bobEmail)));

            Assert.Equal(
                1 + (aliceTransferSucceeded ? 1 : 0) + (bobTransferSucceeded ? 1 : 0),
                await CountMovementsAsync(aliceAccount.Id));
            Assert.Equal(
                1 + (aliceTransferSucceeded ? 1 : 0) + (bobTransferSucceeded ? 1 : 0),
                await CountMovementsAsync(bobAccount.Id));
        }
        finally
        {
            DisposeResponses(responses);
        }
    }
}
