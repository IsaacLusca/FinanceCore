using System.Net;
using FinanceCore.IntegrationTests.Infrastructure;

namespace FinanceCore.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class SecurityTests(FinanceCoreApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task PasswordIsStoredAsHashAndNotLeaked()
    {
        var email = NewEmail();

        var register = await PostJsonAsync(
            "/api/auth/register",
            new { name = "Alice", email, password = Password });

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var responseBody = await register.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Password, responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain("password", responseBody, StringComparison.OrdinalIgnoreCase);

        var passwordHash = await GetPasswordHashAsync(email);
        Assert.NotEqual(Password, passwordHash);
        Assert.StartsWith("AQAAAA", passwordHash);
        Assert.True(passwordHash.Length > 50);
    }

    [Fact]
    public async Task TamperedAndMalformedTokensAreRejected()
    {
        var alice = await RegisterAsync();
        var account = await CreateAccountAsync(alice.AccessToken);

        var parts = alice.AccessToken.Split('.');
        var tampered = $"{parts[0]}.{parts[1]}.{new string('x', parts[2].Length)}";

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await SendAsync(HttpMethod.Get, $"/api/accounts/{account.Id}", tampered)).StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await SendAsync(HttpMethod.Get, "/api/accounts", "not-a-jwt")).StatusCode);
    }
}
