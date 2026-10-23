using System.ComponentModel.DataAnnotations;

namespace FinanceCore.Api.Features.Auth;

public sealed record RegisterRequest(
    [Required, MaxLength(120)] string Name,
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MinLength(8), MaxLength(128)] string Password);

public sealed record LoginRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MaxLength(128)] string Password);

public sealed record AuthResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc);
