using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Northline.Api.Authorization;
using Northline.Api.Dtos;
using Northline.Api.Services;

namespace Northline.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(IAuthService auth, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Authenticate with a seeded account and receive a JWT bearer token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<LoginResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);

    /// <summary>The currently authenticated user.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<UserDto> Me(CancellationToken ct) => auth.GetUserAsync(currentUser.UserId, ct);
}
