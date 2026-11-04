using Microsoft.EntityFrameworkCore;
using Northline.Api.Dtos;
using Northline.Api.Entities;

namespace Northline.Api.Tests.Integration;

/// <summary>Guards the documented initial state (docs/SEED_DATA.md) against accidental seed changes.</summary>
[Collection(PostgresCollection.Name)]
public class SeedDataTests(PostgresFixture fx) : IAsyncLifetime
{
    public Task InitializeAsync() => fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Seed_creates_documented_record_counts()
    {
        await using var s = fx.Scope();
        Assert.Equal(17, await s.Db.TransportOrders.CountAsync());
        Assert.Equal(10, await s.Db.Drivers.CountAsync());
        Assert.Equal(10, await s.Db.Vehicles.CountAsync());
        Assert.Equal(7, await s.Db.DispatchAssignments.CountAsync());
        Assert.Equal(4, await s.Db.Users.CountAsync());
    }

    [Fact]
    public async Task Seed_assignment_statuses_match_documentation()
    {
        await using var s = fx.Scope();
        var statuses = await s.Db.DispatchAssignments.OrderBy(a => a.AssignmentNumber)
            .Select(a => new { a.AssignmentNumber, a.Status, a.PreviousAssignmentId }).ToListAsync();

        Assert.Equal(
            [AssignmentStatus.Assigned, AssignmentStatus.Reassigned, AssignmentStatus.Assigned, AssignmentStatus.Assigned,
             AssignmentStatus.Completed, AssignmentStatus.Cancelled, AssignmentStatus.Assigned],
            statuses.Select(a => a.Status));
        Assert.Equal((await s.Assignment("DA-0002")).Id, statuses.Single(a => a.AssignmentNumber == "DA-0003").PreviousAssignmentId);
    }

    [Fact]
    public async Task Seed_eligible_orders_match_documentation()
    {
        await using var s = fx.Scope();
        var eligible = await s.Orders.ListAsync(new OrderListQuery { EligibleOnly = true, Sort = OrderSortField.OrderNumber }, default);

        Assert.Equal(["TO-1001", "TO-1002", "TO-1003", "TO-1004", "TO-1005", "TO-1014", "TO-1015", "TO-1017"],
            eligible.Items.Select(o => o.OrderNumber));
    }

    [Fact]
    public async Task Candidates_for_heavy_order_only_allow_the_heavy_combination_vehicle()
    {
        await using var s = fx.Scope();
        var candidates = await s.Orders.GetCandidatesAsync(await s.OrderId("TO-1002"), new CandidatesQuery(), default);

        Assert.Equal(["VEH-07"], candidates.Vehicles.Where(v => v.Eligible).Select(v => v.Vehicle.VehicleCode));
    }
}
