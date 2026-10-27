using System.Net;
using System.Net.Http.Json;
using FinanceCore.IntegrationTests.Infrastructure;

namespace FinanceCore.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AuthTests(FinanceCoreApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task RegisterAndLoginReturnUsableToken()
    {
        var email = NewEmail();

        var register = await PostJsonAsync(
            "/api/auth/register",
            new { name = "Alice", email, password = Password });

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var registerAuth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(registerAuth);
        Assert.False(string.IsNullOrWhiteSpace(registerAuth!.AccessToken));
        Assert.Equal("Bearer", registerAuth.TokenType);
        Assert.True(registerAuth.ExpiresAtUtc > DateTime.UtcNow);

        var login = await PostJsonAsync(
            "/api/auth/login",
            new { email, password = Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginAuth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loginAuth);

        var accounts = await SendAsync(HttpMethod.Get, "/api/accounts", loginAuth!.AccessToken);
        Assert.Equal(HttpStatusCode.OK, accounts.StatusCode);
    }

    [Fact]
    public async Task DuplicateEmailAndWrongPasswordAreRejected()
    {
        var email = NewEmail();
        var register = await PostJsonAsync(
            "/api/auth/register",
            new { name = "Alice", email, password = Password });

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var duplicate = await PostJsonAsync(
            "/api/auth/register",
            new { name = "Alice", email, password = Password });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var wrongPassword = await PostJsonAsync(
            "/api/auth/login",
            new { email, password = "SenhaErrada123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
    }
}
