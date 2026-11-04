using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Northline.Api.Authorization;
using Northline.Api.Data;
using Northline.Api.Data.Seed;
using Northline.Api.Entities;
using Northline.Api.Services;
using Northline.Api.Validation;
using Testcontainers.PostgreSql;

namespace Northline.Api.Tests.Integration;

/// <summary>
/// Starts one throwaway PostgreSQL container (requires Docker) and applies the real EF Core migrations,
/// so the partial unique index and exclusion constraints under test are exactly those used in production.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    /// <summary>Fixed clock: 2026-06-01 12:00 NZST.</summary>
    public static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public FakeTimeProvider Clock { get; } = new(Now);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public NorthlineDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<NorthlineDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options);

    /// <summary>Restores the documented seed dataset, anchored to the fixed clock.</summary>
    public async Task ResetAsync()
    {
        Clock.SetUtcNow(Now);
        await using var db = CreateDbContext();
        var seeder = new DatabaseSeeder(db, new PasswordHasher<User>(),
            Options.Create(new SeedOptions { DevPassword = "test-password" }), Clock, NullLogger<DatabaseSeeder>.Instance);
        await seeder.ResetAsync(CancellationToken.None);
    }

    /// <summary>A unit of work equivalent to one HTTP request: its own DbContext and services.</summary>
    public TestScope Scope(int userId = 2) => new(CreateDbContext(), Clock, userId);
}

public sealed class TestScope : IAsyncDisposable
{
    public TestScope(NorthlineDbContext db, TimeProvider clock, int userId)
    {
        Db = db;
        var licenceRules = new LicenceRules(Options.Create(new DispatchOptions()));
        var evaluator = new DispatchRuleEvaluator(licenceRules);
        Assignments = new AssignmentQueryService(db, clock);
        Orders = new OrderQueryService(db, evaluator, licenceRules, Assignments, clock);
        Dispatch = new DispatchService(db, evaluator, Assignments, new FixedUser(userId), clock);
    }

    public NorthlineDbContext Db { get; }
    public IDispatchService Dispatch { get; }
    public IAssignmentQueryService Assignments { get; }
    public IOrderQueryService Orders { get; }

    public async Task<int> OrderId(string number) =>
        (await Db.TransportOrders.AsNoTracking().SingleAsync(o => o.OrderNumber == number)).Id;

    public async Task<int> DriverId(string code) =>
        (await Db.Drivers.AsNoTracking().SingleAsync(d => d.DriverCode == code)).Id;

    public async Task<int> VehicleId(string code) =>
        (await Db.Vehicles.AsNoTracking().SingleAsync(v => v.VehicleCode == code)).Id;

    public Task<DispatchAssignment> Assignment(string number) =>
        Db.DispatchAssignments.AsNoTracking().SingleAsync(a => a.AssignmentNumber == number);

    public Task<TransportOrder> Order(string number) =>
        Db.TransportOrders.AsNoTracking().SingleAsync(o => o.OrderNumber == number);

    public ValueTask DisposeAsync() => Db.DisposeAsync();

    private sealed class FixedUser(int userId) : ICurrentUser
    {
        public int UserId => userId;
        public UserRole Role => UserRole.Dispatcher;
    }
}

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
