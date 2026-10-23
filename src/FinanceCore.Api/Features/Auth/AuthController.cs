using FinanceCore.Api.Common;
using FinanceCore.Api.Data;
using FinanceCore.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceCore.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    FinanceCoreDbContext db,
    IPasswordHasher<User> passwordHasher,
    JwtTokenService tokenService) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var name = request.Name.Trim();

        if (name.Length == 0)
        {
            ModelState.AddModelError(nameof(request.Name), "Informe o nome.");
            return ValidationProblem(ModelState);
        }

        if (await db.Users.AnyAsync(user => user.Email == email, ct))
        {
            return EmailAlreadyRegistered();
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            CreatedAtUtc = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception, "ux_users_email"))
        {
            return EmailAlreadyRegistered();
        }

        return StatusCode(StatusCodes.Status201Created, tokenService.CreateToken(user));
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(candidate => candidate.Email == email, ct);

        if (user is null)
        {
            return InvalidCredentials();
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(ct);
        }

        return Ok(tokenService.CreateToken(user));
    }

    private ObjectResult EmailAlreadyRegistered() =>
        Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "E-mail já cadastrado.",
            detail: "Utilize outro e-mail.");

    private ObjectResult InvalidCredentials() =>
        Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Credenciais inválidas.",
            detail: "E-mail ou senha incorretos.");
}
