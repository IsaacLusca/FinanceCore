using System.Net;
using System.Net.Http.Json;
using FinanceCore.IntegrationTests.Infrastructure;

namespace FinanceCore.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AuthorizationTests(FinanceCoreApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task UserCannotAccessAnotherUsersResources()
    {
        var alice = await RegisterAsync();
        var bob = await RegisterAsync();
        var aliceAccount = await CreateAccountAsync(alice.AccessToken);
        var bobAccount = await CreateAccountAsync(bob.AccessToken);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await SendAsync(HttpMethod.Get, $"/api/accounts/{bobAccount.Id}", alice.AccessToken)).StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await SendAsync(HttpMethod.Get, $"/api/accounts/{bobAccount.Id}/movements", alice.AccessToken)).StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await DepositAsync(alice.AccessToken, bobAccount.Id, 10m, "foreign-deposit")).StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await TransferAsync(alice.AccessToken, bobAccount.Id, aliceAccount.Id, 10m, "foreign-transfer")).StatusCode);

        var listResponse = await SendAsync(HttpMethod.Get, "/api/accounts", alice.AccessToken);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var accounts = await listResponse.Content.ReadFromJsonAsync<List<AccountResponse>>();
        Assert.NotNull(accounts);
        var account = Assert.Single(accounts!);
        Assert.Equal(aliceAccount.Id, account.Id);
        Assert.Equal(0m, (await GetAccountAsync(bob.AccessToken, bobAccount.Id)).Balance);
    }

    [Fact]
    public async Task RequestsWithoutTokenAreRejected()
    {
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await SendAsync(HttpMethod.Get, "/api/accounts")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await SendAsync(HttpMethod.Post, "/api/accounts")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await PostJsonAsync(
                $"/api/accounts/{Guid.NewGuid()}/deposits",
                new { amount = 10m },
                idempotencyKey: "anonymous-deposit")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await PostJsonAsync(
                "/api/transfers",
                new { sourceAccountId = Guid.NewGuid(), destinationAccountId = Guid.NewGuid(), amount = 10m },
                idempotencyKey: "anonymous-transfer")).StatusCode);
    }
}
