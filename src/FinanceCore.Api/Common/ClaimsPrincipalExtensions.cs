using System.Security.Claims;

namespace FinanceCore.Api.Common;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("sub");

        if (!Guid.TryParse(value, out var userId))
        {
            throw new InvalidOperationException("Token sem identificador de usuário válido.");
        }

        return userId;
    }
}
