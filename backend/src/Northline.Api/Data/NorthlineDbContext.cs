using Microsoft.EntityFrameworkCore;
using Northline.Api.Entities;

namespace Northline.Api.Data;

public class NorthlineDbContext(DbContextOptions<NorthlineDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<TransportOrder> TransportOrders => Set<TransportOrder>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<DispatchAssignment> DispatchAssignments => Set<DispatchAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Required by the exclusion constraints on dispatch_assignments (see DbConstraints).
        modelBuilder.HasPostgresExtension("btree_gist");
        // Source of DA-#### assignment numbers.
        modelBuilder.HasSequence<long>(DbConstraints.AssignmentNumberSequence);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NorthlineDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Store enums as readable text so QA can inspect the database directly.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }
}
