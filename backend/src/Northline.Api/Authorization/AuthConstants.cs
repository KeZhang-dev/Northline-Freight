using System.Security.Claims;
using Northline.Api.Entities;

namespace Northline.Api.Authorization;

public static class Policies
{
    /// <summary>View dispatch data: orders, drivers, vehicles, assignments. Admin and Dispatcher.</summary>
    public const string DispatchRead = "DispatchRead";

    /// <summary>Create, reassign, cancel and complete assignments. Admin and Dispatcher.</summary>
    public const string DispatchWrite = "DispatchWrite";

    public const string AdminOnly = "AdminOnly";
    public const string DriverOnly = "DriverOnly";
}

public static class Claims
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
}

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "northline-tms";
    public string Audience { get; set; } = "northline-tms-web";

    /// <summary>HMAC signing key, at least 32 characters. Supplied by configuration, never committed for production.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 480;
}

public interface ICurrentUser
{
    int UserId { get; }
    UserRole Role { get; }
}

public class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal Principal =>
        accessor.HttpContext?.User ?? throw new InvalidOperationException("No HTTP context.");

    public int UserId => int.Parse(Principal.FindFirstValue(Claims.Subject)
        ?? throw new InvalidOperationException("Authenticated user has no subject claim."));

    public UserRole Role => Enum.Parse<UserRole>(Principal.FindFirstValue(Claims.Role)
        ?? throw new InvalidOperationException("Authenticated user has no role claim."));
}
