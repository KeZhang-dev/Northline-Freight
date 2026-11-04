using Microsoft.EntityFrameworkCore;
using Northline.Api.Common;
using Northline.Api.Data;
using Northline.Api.Dtos;
using Northline.Api.Entities;
using Npgsql;

namespace Northline.Api.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class DispatchServiceTests(PostgresFixture fx) : IAsyncLifetime
{
    public Task InitializeAsync() => fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<CreateAssignmentRequest> Request(TestScope s, string order, string driver, string vehicle) => new()
    {
        OrderId = await s.OrderId(order), DriverId = await s.DriverId(driver), VehicleId = await s.VehicleId(vehicle),
    };

    // ---------------- Create ----------------

    [Fact]
    public async Task Valid_assignment_is_persisted_and_order_becomes_assigned()
    {
        await using var s = fx.Scope();
        var created = await s.Dispatch.CreateAsync(await Request(s, "TO-1001", "DRV-005", "VEH-01"), default);

        Assert.Equal("DA-0008", created.AssignmentNumber);
        Assert.Equal(AssignmentStatus.Assigned, created.Status);
        Assert.Equal(2, created.CreatedBy.Id);

        await using var check = fx.Scope();
        Assert.Equal(OrderStatus.Assigned, (await check.Order("TO-1001")).Status);
        var stored = await check.Assignment("DA-0008");
        Assert.Equal((await check.Order("TO-1001")).PickupAt, stored.StartAt);
    }

    [Fact]
    public async Task Failed_assignment_changes_nothing()
    {
        await using var s = fx.Scope();
        var before = await s.Db.DispatchAssignments.CountAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(async () =>
            await s.Dispatch.CreateAsync(await Request(s, "TO-1003", "DRV-002", "VEH-05"), default));

        Assert.Equal(422, ex.StatusCode);
        Assert.Equal([ErrorCodes.DriverScheduleConflict, ErrorCodes.VehicleScheduleConflict], ex.Violations.Select(v => v.Code));

        await using var check = fx.Scope();
        Assert.Equal(before, await check.Db.DispatchAssignments.CountAsync());
        Assert.Equal(OrderStatus.Pending, (await check.Order("TO-1003")).Status);
    }

    [Fact]
    public async Task Second_active_assignment_for_an_order_is_rejected()
    {
        await using var s = fx.Scope();
        var ex = await Assert.ThrowsAsync<DomainException>(async () =>
            await s.Dispatch.CreateAsync(await Request(s, "TO-1009", "DRV-001", "VEH-06"), default));

        Assert.Equal(409, ex.StatusCode);
        Assert.Equal(ErrorCodes.OrderAlreadyAssigned, ex.Code);
    }

    [Fact]
    public async Task Back_to_back_assignment_is_allowed()
    {
        await using var s = fx.Scope();
        var created = await s.Dispatch.CreateAsync(await Request(s, "TO-1015", "DRV-002", "VEH-05"), default);
        Assert.Equal(AssignmentStatus.Assigned, created.Status);
    }

    [Fact]
    public async Task Missing_resources_return_not_found()
    {
        await using var s = fx.Scope();
        var ex = await Assert.ThrowsAsync<DomainException>(async () =>
            await s.Dispatch.CreateAsync(new CreateAssignmentRequest { OrderId = await s.OrderId("TO-1001"), DriverId = 999, VehicleId = 1 }, default));
        Assert.Equal(ErrorCodes.DriverNotFound, ex.Code);
    }

    // ---------------- Cancel / complete ----------------

    [Fact]
    public async Task Cancel_closes_assignment_returns_order_to_pending_and_releases_resources()
    {
        await using (var s = fx.Scope())
        {
            var da1 = await s.Assignment("DA-0001");
            var cancelled = await s.Dispatch.CancelAsync(da1.Id, new CancelAssignmentRequest { Version = da1.Version, Reason = "  Customer delay  " }, default);

            Assert.Equal(AssignmentStatus.Cancelled, cancelled.Status);
            Assert.Equal("Customer delay", cancelled.CloseReason);
            Assert.Equal(PostgresFixture.Now, cancelled.ClosedAt);
            Assert.Equal(2, cancelled.ClosedBy!.Id);
            Assert.Equal(OrderStatus.Pending, (await s.Order("TO-1009")).Status);
        }

        // DRV-002 / VEH-05 were blocked for TO-1003 by DA-0001; they are now free.
        await using var s2 = fx.Scope();
        var created = await s2.Dispatch.CreateAsync(await Request(s2, "TO-1003", "DRV-002", "VEH-05"), default);
        Assert.Equal(AssignmentStatus.Assigned, created.Status);
    }

    [Fact]
    public async Task Complete_closes_assignment_and_completes_order_and_cannot_be_repeated()
    {
        await using var s = fx.Scope();
        var da4 = await s.Assignment("DA-0004");
        var completed = await s.Dispatch.CompleteAsync(da4.Id, new CompleteAssignmentRequest { Version = da4.Version }, default);

        Assert.Equal(AssignmentStatus.Completed, completed.Status);
        Assert.Null(completed.CloseReason);
        Assert.Equal(OrderStatus.Completed, (await s.Order("TO-1011")).Status);

        await using var s2 = fx.Scope();
        var fresh = await s2.Assignment("DA-0004");
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            s2.Dispatch.CompleteAsync(fresh.Id, new CompleteAssignmentRequest { Version = fresh.Version }, default));
        Assert.Equal(ErrorCodes.InvalidStatusTransition, ex.Code);
    }

    [Theory]
    [InlineData("DA-0002")] // Reassigned
    [InlineData("DA-0005")] // Completed
    [InlineData("DA-0006")] // Cancelled
    public async Task Closed_assignments_reject_every_action(string number)
    {
        await using var s = fx.Scope();
        var a = await s.Assignment(number);

        var cancel = await Assert.ThrowsAsync<DomainException>(() =>
            s.Dispatch.CancelAsync(a.Id, new CancelAssignmentRequest { Version = a.Version, Reason = "x" }, default));
        var complete = await Assert.ThrowsAsync<DomainException>(() =>
            s.Dispatch.CompleteAsync(a.Id, new CompleteAssignmentRequest { Version = a.Version }, default));
        var reassign = await Assert.ThrowsAsync<DomainException>(() =>
            s.Dispatch.ReassignAsync(a.Id, new ReassignAssignmentRequest { Version = a.Version, DriverId = 1, Reason = "x" }, default));

        Assert.All([cancel, complete, reassign], e => Assert.Equal(ErrorCodes.InvalidStatusTransition, e.Code));
    }

    [Fact]
    public async Task Stale_version_is_rejected_without_changes()
    {
        await using var s = fx.Scope();
        var da7 = await s.Assignment("DA-0007");

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            s.Dispatch.CancelAsync(da7.Id, new CancelAssignmentRequest { Version = da7.Version + 1, Reason = "x" }, default));

        Assert.Equal(ErrorCodes.AssignmentModified, ex.Code);
        await using var check = fx.Scope();
        Assert.Equal(AssignmentStatus.Assigned, (await check.Assignment("DA-0007")).Status);
    }

    // ---------------- Reassign ----------------

    [Fact]
    public async Task Reassign_preserves_old_record_and_links_new_one()
    {
        await using var s = fx.Scope(userId: 3);
        var da7 = await s.Assignment("DA-0007");
        var result = await s.Dispatch.ReassignAsync(da7.Id,
            new ReassignAssignmentRequest { Version = da7.Version, DriverId = await s.DriverId("DRV-003"), Reason = "Driver swap" }, default);

        Assert.Equal(AssignmentStatus.Reassigned, result.Previous.Status);
        Assert.Equal("Driver swap", result.Previous.CloseReason);
        Assert.Equal(3, result.Previous.ClosedBy!.Id);
        Assert.Equal(result.Current.Id, result.Previous.ReplacedByAssignmentId);

        Assert.Equal(AssignmentStatus.Assigned, result.Current.Status);
        Assert.Equal(da7.Id, result.Current.PreviousAssignmentId);
        Assert.Equal("DRV-003", result.Current.Driver.DriverCode);
        Assert.Equal(da7.VehicleId, result.Current.Vehicle.Id);
        Assert.Equal(3, result.Current.CreatedBy.Id);

        await using var check = fx.Scope();
        Assert.Equal(OrderStatus.Assigned, (await check.Order("TO-1016")).Status);
        var detail = await check.Assignments.GetAsync(result.Current.Id, default);
        Assert.Equal(["DA-0007", result.Current.AssignmentNumber], detail.History.Select(h => h.AssignmentNumber));
    }

    [Fact]
    public async Task Reassign_excludes_the_replaced_assignment_from_conflict_checks()
    {
        // Same driver, same window, different vehicle: DA-0001 must not conflict with itself.
        await using var s = fx.Scope();
        var da1 = await s.Assignment("DA-0001");
        var result = await s.Dispatch.ReassignAsync(da1.Id,
            new ReassignAssignmentRequest { Version = da1.Version, VehicleId = await s.VehicleId("VEH-06"), Reason = "Vehicle swap" }, default);

        Assert.Equal("DRV-002", result.Current.Driver.DriverCode);
        Assert.Equal("VEH-06", result.Current.Vehicle.VehicleCode);
    }

    [Fact]
    public async Task Reassign_without_change_or_to_conflicting_resource_is_rejected()
    {
        await using var s = fx.Scope();
        var da3 = await s.Assignment("DA-0003");

        var noChange = await Assert.ThrowsAsync<DomainException>(() => s.Dispatch.ReassignAsync(da3.Id,
            new ReassignAssignmentRequest { Version = da3.Version, DriverId = da3.DriverId, Reason = "x" }, default));
        Assert.Equal([ErrorCodes.ReassignNoChange], noChange.Violations.Select(v => v.Code));

        // DA-0003 is D+2 07-13; DRV-006 is on leave, VEH-03 (6,000 kg) is free and big enough.
        var invalid = await Assert.ThrowsAsync<DomainException>(async () => await s.Dispatch.ReassignAsync(da3.Id,
            new ReassignAssignmentRequest { Version = da3.Version, DriverId = await s.DriverId("DRV-006"), Reason = "x" }, default));
        Assert.Equal([ErrorCodes.DriverNotActive], invalid.Violations.Select(v => v.Code));

        await using var check = fx.Scope();
        Assert.Equal(AssignmentStatus.Assigned, (await check.Assignment("DA-0003")).Status);
    }

    // ---------------- Database constraints (final authority) ----------------

    [Fact]
    public async Task Database_rejects_overlapping_driver_assignment_written_directly()
    {
        await using var s = fx.Scope();
        var da1 = await s.Assignment("DA-0001");
        var exception = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            s.Db.DispatchAssignments.Add(new DispatchAssignment
            {
                AssignmentNumber = "DA-X1", OrderId = await s.OrderId("TO-1003"), DriverId = da1.DriverId,
                VehicleId = await s.VehicleId("VEH-06"), StartAt = da1.StartAt.AddHours(1), EndAt = da1.EndAt.AddHours(1),
                Status = AssignmentStatus.Assigned, CreatedAt = PostgresFixture.Now, CreatedByUserId = 2,
            });
            await s.Db.SaveChangesAsync();
        });

        var pg = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, pg.SqlState);
        Assert.Equal(DbConstraints.DriverOverlap, pg.ConstraintName);
    }

    [Fact]
    public async Task Database_rejects_second_active_assignment_for_order_written_directly()
    {
        await using var s = fx.Scope();
        var da1 = await s.Assignment("DA-0001");
        var exception = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            s.Db.DispatchAssignments.Add(new DispatchAssignment
            {
                AssignmentNumber = "DA-X2", OrderId = da1.OrderId, DriverId = await s.DriverId("DRV-001"),
                VehicleId = await s.VehicleId("VEH-06"), StartAt = da1.StartAt, EndAt = da1.EndAt,
                Status = AssignmentStatus.Assigned, CreatedAt = PostgresFixture.Now, CreatedByUserId = 2,
            });
            await s.Db.SaveChangesAsync();
        });

        var pg = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, pg.SqlState);
        Assert.Equal(DbConstraints.ActiveOrderUnique, pg.ConstraintName);
    }

    [Fact]
    public async Task Database_allows_overlap_with_closed_assignments()
    {
        // DA-0006 (Cancelled) used DRV-010 / VEH-01 in TO-1014's window.
        await using var s = fx.Scope();
        var created = await s.Dispatch.CreateAsync(await Request(s, "TO-1014", "DRV-010", "VEH-01"), default);
        Assert.Equal(AssignmentStatus.Assigned, created.Status);
    }

    // ---------------- Concurrency ----------------

    private static async Task<(int Successes, List<DomainException> Failures)> RunConcurrently(params Func<Task>[] operations)
    {
        var failures = new List<DomainException>();
        var successes = 0;
        using var start = new ManualResetEventSlim(false);
        var tasks = operations.Select(op => Task.Run(async () =>
        {
            start.Wait();
            try
            {
                await op();
                Interlocked.Increment(ref successes);
            }
            catch (DomainException ex)
            {
                lock (failures) failures.Add(ex);
            }
        })).ToArray();
        start.Set();
        await Task.WhenAll(tasks);
        return (successes, failures);
    }

    [Fact]
    public async Task Concurrent_requests_cannot_double_book_a_driver()
    {
        for (var i = 0; i < 5; i++)
        {
            await fx.ResetAsync();
            await using var s1 = fx.Scope();
            await using var s2 = fx.Scope(userId: 3);
            // TO-1001 (D+1 08-12) and TO-1003 (D+1 10-13) overlap; both requested for DRV-001.
            var r1 = await Request(s1, "TO-1001", "DRV-001", "VEH-02");
            var r2 = await Request(s2, "TO-1003", "DRV-001", "VEH-06");

            var (successes, failures) = await RunConcurrently(
                () => s1.Dispatch.CreateAsync(r1, default),
                () => s2.Dispatch.CreateAsync(r2, default));

            Assert.Equal(1, successes);
            Assert.Equal(ErrorCodes.DriverScheduleConflict, Assert.Single(failures).Violations.Single().Code);

            await using var check = fx.Scope();
            var driverId = await check.DriverId("DRV-001");
            var tomorrow = PostgresFixture.Now.AddDays(1);
            Assert.Equal(1, await check.Db.DispatchAssignments.CountAsync(a =>
                a.DriverId == driverId && a.Status == AssignmentStatus.Assigned && a.StartAt < tomorrow.AddDays(1) && a.StartAt > PostgresFixture.Now));
            Assert.Equal(1, await check.Db.TransportOrders.CountAsync(o =>
                (o.OrderNumber == "TO-1001" || o.OrderNumber == "TO-1003") && o.Status == OrderStatus.Assigned));
        }
    }

    [Fact]
    public async Task Concurrent_requests_cannot_assign_the_same_order_twice()
    {
        for (var i = 0; i < 5; i++)
        {
            await fx.ResetAsync();
            await using var s1 = fx.Scope();
            await using var s2 = fx.Scope(userId: 3);
            var r1 = await Request(s1, "TO-1001", "DRV-005", "VEH-01");
            var r2 = await Request(s2, "TO-1001", "DRV-010", "VEH-02");

            var (successes, failures) = await RunConcurrently(
                () => s1.Dispatch.CreateAsync(r1, default),
                () => s2.Dispatch.CreateAsync(r2, default));

            Assert.Equal(1, successes);
            Assert.Equal(ErrorCodes.OrderAlreadyAssigned, Assert.Single(failures).Code);

            await using var check = fx.Scope();
            var orderId = await check.OrderId("TO-1001");
            Assert.Equal(1, await check.Db.DispatchAssignments.CountAsync(a => a.OrderId == orderId));
        }
    }

    [Fact]
    public async Task Concurrent_cancel_and_reassign_of_the_same_assignment_leave_consistent_state()
    {
        for (var i = 0; i < 5; i++)
        {
            await fx.ResetAsync();
            await using var s1 = fx.Scope();
            await using var s2 = fx.Scope(userId: 3);
            var da7 = await s1.Assignment("DA-0007");
            var newDriver = await s1.DriverId("DRV-003");

            var (successes, failures) = await RunConcurrently(
                () => s1.Dispatch.CancelAsync(da7.Id, new CancelAssignmentRequest { Version = da7.Version, Reason = "Cancel" }, default),
                () => s2.Dispatch.ReassignAsync(da7.Id, new ReassignAssignmentRequest { Version = da7.Version, DriverId = newDriver, Reason = "Swap" }, default));

            Assert.Equal(1, successes);
            Assert.Equal(ErrorCodes.AssignmentModified, Assert.Single(failures).Code);

            await using var check = fx.Scope();
            var order = await check.Order("TO-1016");
            var forOrder = await check.Db.DispatchAssignments.AsNoTracking().Where(a => a.OrderId == order.Id).ToListAsync();
            var closed = forOrder.Single(a => a.Id == da7.Id);
            if (closed.Status == AssignmentStatus.Cancelled)
            {
                Assert.Single(forOrder);
                Assert.Equal(OrderStatus.Pending, order.Status);
            }
            else
            {
                Assert.Equal(AssignmentStatus.Reassigned, closed.Status);
                Assert.Equal(2, forOrder.Count);
                Assert.Equal(OrderStatus.Assigned, order.Status);
            }
        }
    }
}
