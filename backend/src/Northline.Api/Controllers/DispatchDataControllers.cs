using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Northline.Api.Authorization;
using Northline.Api.Dtos;
using Northline.Api.Services;

namespace Northline.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Produces("application/json")]
[Authorize(Policy = Policies.DispatchRead)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class OrdersController(IOrderQueryService orders) : ControllerBase
{
    /// <summary>List transport orders. Each order includes its BR-01 eligibility and reasons.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<OrderDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<OrderDto>> List([FromQuery] OrderListQuery query, CancellationToken ct) => orders.ListAsync(query, ct);

    /// <summary>Order detail with its full assignment history.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<OrderDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<OrderDetailDto> Get(int id, CancellationToken ct) => orders.GetAsync(id, ct);

    /// <summary>
    /// Every driver and vehicle with its eligibility for this order and the reasons it fails.
    /// Pass driverId / vehicleId to include licence compatibility (BR-07) against the selected counterpart,
    /// and excludeAssignmentId for reassignment. This is an advisory view: POST /api/assignments
    /// validates independently.
    /// </summary>
    [HttpGet("{id:int}/candidates")]
    [ProducesResponseType<CandidatesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<CandidatesDto> Candidates(int id, [FromQuery] CandidatesQuery query, CancellationToken ct) =>
        orders.GetCandidatesAsync(id, query, ct);
}

[ApiController]
[Route("api/drivers")]
[Produces("application/json")]
[Authorize(Policy = Policies.DispatchRead)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class DriversController(IResourceQueryService resources) : ControllerBase
{
    /// <summary>List drivers with derived availability and current/next assignment.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DriverDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<DriverDto>> List([FromQuery] DriverListQuery query, CancellationToken ct) =>
        resources.ListDriversAsync(query, ct);

    /// <summary>Driver detail with all of the driver's assignments.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<DriverDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<DriverDetailDto> Get(int id, CancellationToken ct) => resources.GetDriverAsync(id, ct);
}

[ApiController]
[Route("api/vehicles")]
[Produces("application/json")]
[Authorize(Policy = Policies.DispatchRead)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class VehiclesController(IResourceQueryService resources) : ControllerBase
{
    /// <summary>List vehicles with derived availability and current/next assignment.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<VehicleDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<VehicleDto>> List([FromQuery] VehicleListQuery query, CancellationToken ct) =>
        resources.ListVehiclesAsync(query, ct);

    /// <summary>Vehicle detail with all of the vehicle's assignments.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<VehicleDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<VehicleDetailDto> Get(int id, CancellationToken ct) => resources.GetVehicleAsync(id, ct);
}

[ApiController]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class SummaryController(ISummaryService summary, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Dispatch overview KPI counts.</summary>
    [HttpGet("api/dispatch/summary")]
    [Authorize(Policy = Policies.DispatchRead)]
    [ProducesResponseType<DispatchSummaryDto>(StatusCodes.Status200OK)]
    public Task<DispatchSummaryDto> DispatchSummary(CancellationToken ct) => summary.GetDispatchSummaryAsync(ct);

    /// <summary>Sample operational statistics for the dashboard.</summary>
    [HttpGet("api/dashboard/stats")]
    [Authorize(Policy = Policies.DispatchRead)]
    [ProducesResponseType<DashboardStatsDto>(StatusCodes.Status200OK)]
    public Task<DashboardStatsDto> DashboardStats(CancellationToken ct) => summary.GetDashboardStatsAsync(ct);

    /// <summary>Profile for the restricted driver landing page.</summary>
    [HttpGet("api/driver/me")]
    [Authorize(Policy = Policies.DriverOnly)]
    [ProducesResponseType<DriverProfileDto>(StatusCodes.Status200OK)]
    public Task<DriverProfileDto> DriverProfile(CancellationToken ct) => summary.GetDriverProfileAsync(currentUser.UserId, ct);
}
