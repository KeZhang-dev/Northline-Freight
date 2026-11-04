using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Northline.Api.Authorization;
using Northline.Api.Common;
using Northline.Api.Data.Seed;
using Northline.Api.Dtos;

namespace Northline.Api.Controllers;

/// <summary>Development-only utilities. Every endpoint returns 404 outside the Development environment.</summary>
[ApiController]
[Route("api/dev")]
[Produces("application/json")]
[Authorize(Policy = Policies.AdminOnly)]
public class DevController(IDatabaseSeeder seeder, IWebHostEnvironment env) : ControllerBase
{
    /// <summary>
    /// Deletes all data and restores the documented seed dataset, with dates re-anchored to now.
    /// Development environment and Admin role only. Seeded user IDs are deterministic, so existing
    /// tokens remain valid after a reset.
    /// </summary>
    [HttpPost("reset")]
    [ProducesResponseType<ResetResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ResetResultDto> Reset(CancellationToken ct)
    {
        if (!env.IsDevelopment())
        {
            throw DomainException.NotFound(ErrorCodes.NotFound, "Not found.");
        }
        return seeder.ResetAsync(ct);
    }
}

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    /// <summary>Liveness check.</summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get() => Ok(new { status = "ok" });
}
