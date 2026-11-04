using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Northline.Api.Dtos;
using Northline.Api.Entities;

namespace Northline.Api.Data.Seed;

public interface IDatabaseSeeder
{
    /// <summary>Applies pending migrations.</summary>
    Task MigrateAsync(CancellationToken ct);

    /// <summary>Seeds the development dataset if the database contains no users.</summary>
    Task SeedIfEmptyAsync(CancellationToken ct);

    /// <summary>Deletes all data and re-seeds, in a single transaction.</summary>
    Task<ResetResultDto> ResetAsync(CancellationToken ct);
}

public class DatabaseSeeder(
    NorthlineDbContext db,
    IPasswordHasher<User> hasher,
    IOptions<SeedOptions> seedOptions,
    TimeProvider clock,
    ILogger<DatabaseSeeder> logger) : IDatabaseSeeder
{
    public async Task MigrateAsync(CancellationToken ct)
    {
        await db.Database.MigrateAsync(ct);
    }

    public async Task SeedIfEmptyAsync(CancellationToken ct)
    {
        if (await db.Users.AnyAsync(ct))
        {
            logger.LogInformation("Database already contains data; skipping seed.");
            return;
        }
        await ResetAsync(ct);
    }

    public async Task<ResetResultDto> ResetAsync(CancellationToken ct)
    {
        var password = seedOptions.Value.DevPassword;
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Seed:DevPassword is not configured. Set it in appsettings.Development.json or the SEED__DEVPASSWORD environment variable.");
        }

        var now = clock.GetUtcNow();
        var data = SeedData.Build(now, password, hasher);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE dispatch_assignments, users, transport_orders, drivers, vehicles RESTART IDENTITY CASCADE;", ct);

        db.ChangeTracker.Clear();
        db.Drivers.AddRange(data.Drivers);
        db.Vehicles.AddRange(data.Vehicles);
        db.Users.AddRange(data.Users);
        db.TransportOrders.AddRange(data.Orders);
        await db.SaveChangesAsync(ct);

        // Insert assignments one at a time in number order so identity values match DA numbers and the
        // reassignment chain (DA-0002 -> DA-0003) has its predecessor saved first.
        foreach (var assignment in data.Assignments.OrderBy(a => a.AssignmentNumber))
        {
            db.DispatchAssignments.Add(assignment);
            await db.SaveChangesAsync(ct);
        }

        // The next assignment created through the API is DA-0008.
        await db.Database.ExecuteSqlRawAsync(
            $"SELECT setval('{DbConstraints.AssignmentNumberSequence}', {SeedData.SeededAssignmentCount});", ct);

        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();

        logger.LogInformation("Database reset and seeded at {Now}.", now);
        return new ResetResultDto(now, data.Orders.Count, data.Drivers.Count, data.Vehicles.Count,
            data.Assignments.Count, data.Users.Count);
    }
}
