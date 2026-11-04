using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Northline.Api.Authorization;
using Northline.Api.Dtos;
using Northline.Api.Services;

namespace Northline.Api.Controllers;

/// <summary>Dispatch assignments: create, reassign, cancel, complete and view.</summary>
[ApiController]
[Route("api/assignments")]
[Produces("application/json")]
[Authorize(Policy = Policies.DispatchRead)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class AssignmentsController(IDispatchService dispatch, IAssignmentQueryService queries) : ControllerBase
{
    /// <summary>List assignments with filtering, search, sorting and paging.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<AssignmentDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<AssignmentDto>> List([FromQuery] AssignmentListQuery query, CancellationToken ct) =>
        queries.ListAsync(query, ct);

    /// <summary>Assignment detail, including every assignment for the same order (reassignment history).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<AssignmentDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<AssignmentDetailDto> Get(int id, CancellationToken ct) => queries.GetAsync(id, ct);

    /// <summary>Assign a driver and vehicle to a pending order (BR-01 to BR-10).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.DispatchWrite)]
    [ProducesResponseType<AssignmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AssignmentDto>> Create(CreateAssignmentRequest request, CancellationToken ct)
    {
        var created = await dispatch.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>
    /// Reassign an active assignment to a different driver and/or vehicle (BR-11). The current record is
    /// closed as Reassigned and a new Assigned record is created, linked through previousAssignmentId.
    /// </summary>
    [HttpPost("{id:int}/reassign")]
    [Authorize(Policy = Policies.DispatchWrite)]
    [ProducesResponseType<ReassignmentResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReassignmentResultDto>> Reassign(int id, ReassignAssignmentRequest request, CancellationToken ct)
    {
        var result = await dispatch.ReassignAsync(id, request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Current.Id }, result);
    }

    /// <summary>Cancel an active assignment (BR-12). The order returns to Pending.</summary>
    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = Policies.DispatchWrite)]
    [ProducesResponseType<AssignmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<AssignmentDto> Cancel(int id, CancelAssignmentRequest request, CancellationToken ct) =>
        dispatch.CancelAsync(id, request, ct);

    /// <summary>Mark an active assignment as completed. The order becomes Completed.</summary>
    [HttpPost("{id:int}/complete")]
    [Authorize(Policy = Policies.DispatchWrite)]
    [ProducesResponseType<AssignmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<AssignmentDto> Complete(int id, CompleteAssignmentRequest request, CancellationToken ct) =>
        dispatch.CompleteAsync(id, request, ct);
}
