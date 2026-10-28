using System.Net;
using System.Net.Http.Json;
using FinanceCore.Api.Common;
using FinanceCore.IntegrationTests.Infrastructure;

namespace FinanceCore.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ValidationTests(FinanceCoreApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task InvalidRegistrationPayloadsAreRejected()
    {
        var invalidEmail = await PostJsonAsync(
            "/api/auth/register",
            new { name = "Alice", email = "not-an-email", password = Password });
        Assert.Equal(HttpStatusCode.BadRequest, invalidEmail.StatusCode);

        var shortPassword = await PostJsonAsync(
            "/api/auth/register",
            new { name = "Alice", email = NewEmail(), password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, shortPassword.StatusCode);

        var blankName = await PostJsonAsync(
            "/api/auth/register",
            new { name = "   ", email = NewEmail(), password = Password });
        Assert.Equal(HttpStatusCode.BadRequest, blankName.StatusCode);

        var missingFields = await PostJsonAsync("/api/auth/register", new { });
        Assert.Equal(HttpStatusCode.BadRequest, missingFields.StatusCode);
    }

    [Fact]
    public async Task DepositAtMaximumAmountIsAccepted()
    {
        var alice = await RegisterAsync();
        var account = await CreateAccountAsync(alice.AccessToken);

        var deposit = await DepositAsync(alice.AccessToken, account.Id, MoneyRules.MaxAmount, "max-amount");

        Assert.Equal(HttpStatusCode.Created, deposit.StatusCode);
        Assert.Equal(MoneyRules.MaxAmount, (await GetAccountAsync(alice.AccessToken, account.Id)).Balance);
        Assert.Equal(MoneyRules.MaxAmount, await GetLedgerSumAsync(account.Id));
    }

    [Fact]
    public async Task DepositAboveMaximumAmountIsRejected()
    {
        var alice = await RegisterAsync();
        var account = await CreateAccountAsync(alice.AccessToken);

        var deposit = await DepositAsync(
            alice.AccessToken,
            account.Id,
            MoneyRules.MaxAmount + 0.01m,
            "above-max-amount");

        Assert.Equal(HttpStatusCode.BadRequest, deposit.StatusCode);
        Assert.Equal(0m, (await GetAccountAsync(alice.AccessToken, account.Id)).Balance);
        Assert.Empty(await GetMovementsAsync(alice.AccessToken, account.Id));
    }

    [Fact]
    public async Task DepositToUnknownAccountReturnsNotFound()
    {
        var aliceEmail = NewEmail();
        var alice = await RegisterAsync(aliceEmail);

        var deposit = await DepositAsync(alice.AccessToken, Guid.NewGuid(), 10m, "unknown-account");

        Assert.Equal(HttpStatusCode.NotFound, deposit.StatusCode);
        Assert.Equal(0, await CountOperationsAsync(await GetUserIdByEmailAsync(aliceEmail)));
    }

    [Fact]
    public async Task TransferWithInvalidAccountsIsRejected()
    {
        var alice = await RegisterAsync();
        var bob = await RegisterAsync();
        var source = await CreateAccountAsync(alice.AccessToken);
        var destination = await CreateAccountAsync(bob.AccessToken);

        Assert.Equal(
            HttpStatusCode.Created,
            (await DepositAsync(alice.AccessToken, source.Id, 10m, "invalid-transfer-deposit")).StatusCode);

        var sameAccount = await TransferAsync(alice.AccessToken, source.Id, source.Id, 5m, "same-account");
        Assert.Equal(HttpStatusCode.BadRequest, sameAccount.StatusCode);

        var unknownSource = await TransferAsync(alice.AccessToken, Guid.NewGuid(), destination.Id, 5m, "unknown-source");
        Assert.Equal(HttpStatusCode.NotFound, unknownSource.StatusCode);

        var unknownDestination = await TransferAsync(alice.AccessToken, source.Id, Guid.NewGuid(), 5m, "unknown-destination");
        Assert.Equal(HttpStatusCode.NotFound, unknownDestination.StatusCode);

        Assert.Equal(10m, (await GetAccountAsync(alice.AccessToken, source.Id)).Balance);
        Assert.Equal(0m, (await GetAccountAsync(bob.AccessToken, destination.Id)).Balance);
    }

    [Fact]
    public async Task MovementHistoryValidatesPagination()
    {
        var alice = await RegisterAsync();
        var account = await CreateAccountAsync(alice.AccessToken);

        Assert.Equal(
            HttpStatusCode.Created,
            (await DepositAsync(alice.AccessToken, account.Id, 10m, "pagination-deposit-1")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Created,
            (await DepositAsync(alice.AccessToken, account.Id, 20m, "pagination-deposit-2")).StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await SendAsync(HttpMethod.Get, $"/api/accounts/{account.Id}/movements?page=0", alice.AccessToken)).StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await SendAsync(HttpMethod.Get, $"/api/accounts/{account.Id}/movements?pageSize=0", alice.AccessToken)).StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await SendAsync(HttpMethod.Get, $"/api/accounts/{account.Id}/movements?pageSize=101", alice.AccessToken)).StatusCode);

        var secondPage = await SendAsync(
            HttpMethod.Get,
            $"/api/accounts/{account.Id}/movements?page=2&pageSize=1",
            alice.AccessToken);
        Assert.Equal(HttpStatusCode.OK, secondPage.StatusCode);
        var secondPageMovements = await secondPage.Content.ReadFromJsonAsync<List<MovementResponse>>();
        Assert.NotNull(secondPageMovements);
        Assert.Single(secondPageMovements!);

        var beyondEnd = await SendAsync(
            HttpMethod.Get,
            $"/api/accounts/{account.Id}/movements?page=99&pageSize=50",
            alice.AccessToken);
        Assert.Equal(HttpStatusCode.OK, beyondEnd.StatusCode);
        var beyondEndMovements = await beyondEnd.Content.ReadFromJsonAsync<List<MovementResponse>>();
        Assert.NotNull(beyondEndMovements);
        Assert.Empty(beyondEndMovements!);
    }
}
