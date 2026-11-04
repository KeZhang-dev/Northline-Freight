using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Northline.Api.Authorization;
using Northline.Api.Common;
using Northline.Api.Data;
using Northline.Api.Dtos;
using Northline.Api.Entities;

namespace Northline.Api.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<UserDto> GetUserAsync(int userId, CancellationToken ct);
}

public class AuthService(
    NorthlineDbContext db,
    IPasswordHasher<User> passwordHasher,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider clock) : IAuthService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email, ct);

        // Same response for unknown email, wrong password and disabled account, to avoid account enumeration.
        if (user is null || !user.IsActive ||
            passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            throw new DomainException(StatusCodes.Status401Unauthorized, ErrorCodes.InvalidCredentials,
                "Authentication failed", "The email address or password is incorrect.");
        }

        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(_jwt.ExpiryMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(Claims.Subject, user.Id.ToString()),
                new Claim(Claims.Email, user.Email),
                new Claim(Claims.Name, user.FullName),
                new Claim(Claims.Role, user.Role.ToString()),
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey)), SecurityAlgorithms.HmacSha256),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new LoginResponse(token, expires, user.ToDto());
    }

    public async Task<UserDto> GetUserAsync(int userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new DomainException(StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized,
                "Unauthorized", "The authenticated user no longer exists.");
        return user.ToDto();
    }
}
