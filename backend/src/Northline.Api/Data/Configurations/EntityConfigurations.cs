using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Northline.Api.Entities;

namespace Northline.Api.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.Email).HasMaxLength(200).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.PasswordHash).IsRequired();

        builder.HasOne(u => u.Driver).WithMany().HasForeignKey(u => u.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(u => u.DriverId).IsUnique();
    }
}

public class TransportOrderConfiguration : IEntityTypeConfiguration<TransportOrder>
{
    public void Configure(EntityTypeBuilder<TransportOrder> builder)
    {
        builder.Property(o => o.OrderNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.Property(o => o.CustomerName).HasMaxLength(200).IsRequired();
        builder.Property(o => o.PickupLocation).HasMaxLength(200).IsRequired();
        builder.Property(o => o.DeliveryLocation).HasMaxLength(200).IsRequired();
        builder.Property(o => o.CargoDescription).HasMaxLength(500).IsRequired();
        builder.Property(o => o.CargoWeightKg).HasPrecision(10, 2);
        builder.HasIndex(o => o.Status);

        // Deliberately no check constraint on PickupAt < DeliveryDeadline: an order with an invalid
        // time window is a seeded negative scenario that BR-01 must reject at assignment time.

        builder.Property(o => o.Version).IsRowVersion();
    }
}

public class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.Property(d => d.DriverCode).HasMaxLength(20).IsRequired();
        builder.HasIndex(d => d.DriverCode).IsUnique();
        builder.Property(d => d.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.LastName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Phone).HasMaxLength(30);
        builder.Property(d => d.Email).HasMaxLength(200);
        builder.Ignore(d => d.FullName);
        builder.ToTable(t => t.HasCheckConstraint("ck_driver_licence_class", "licence_class BETWEEN 1 AND 5"));
    }
}

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.Property(v => v.VehicleCode).HasMaxLength(20).IsRequired();
        builder.HasIndex(v => v.VehicleCode).IsUnique();
        builder.Property(v => v.RegistrationNumber).HasMaxLength(10).IsRequired();
        builder.HasIndex(v => v.RegistrationNumber).IsUnique();
        builder.Property(v => v.MaxPayloadKg).HasPrecision(10, 2);
        builder.ToTable(t => t.HasCheckConstraint("ck_vehicle_payload_positive", "max_payload_kg > 0"));
    }
}

public class DispatchAssignmentConfiguration : IEntityTypeConfiguration<DispatchAssignment>
{
    public void Configure(EntityTypeBuilder<DispatchAssignment> builder)
    {
        builder.Property(a => a.AssignmentNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(a => a.AssignmentNumber).IsUnique();
        builder.Property(a => a.Notes).HasMaxLength(1000);
        builder.Property(a => a.CloseReason).HasMaxLength(500);

        builder.HasOne(a => a.Order).WithMany(o => o.Assignments).HasForeignKey(a => a.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Driver).WithMany(d => d.Assignments).HasForeignKey(a => a.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Vehicle).WithMany(v => v.Assignments).HasForeignKey(a => a.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.CreatedBy).WithMany().HasForeignKey(a => a.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.ClosedBy).WithMany().HasForeignKey(a => a.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.PreviousAssignment).WithMany().HasForeignKey(a => a.PreviousAssignmentId).OnDelete(DeleteBehavior.Restrict);

        // BR-10: at most one active assignment per order.
        builder.HasIndex(a => a.OrderId)
            .IsUnique()
            .HasFilter("status = 'Assigned'")
            .HasDatabaseName(DbConstraints.ActiveOrderUnique);

        // An assignment can be replaced at most once, keeping reassignment history a simple chain.
        builder.HasIndex(a => a.PreviousAssignmentId)
            .IsUnique()
            .HasFilter("previous_assignment_id IS NOT NULL")
            .HasDatabaseName(DbConstraints.PreviousAssignmentUnique);

        builder.HasIndex(a => a.DriverId);
        builder.HasIndex(a => a.VehicleId);
        builder.HasIndex(a => a.Status);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(DbConstraints.AssignmentWindow, "end_at > start_at");
            t.HasCheckConstraint(DbConstraints.AssignmentClosedFields,
                "(status = 'Assigned' AND closed_at IS NULL AND closed_by_user_id IS NULL AND close_reason IS NULL) OR " +
                "(status = 'Completed' AND closed_at IS NOT NULL AND closed_by_user_id IS NOT NULL) OR " +
                "(status IN ('Cancelled', 'Reassigned') AND closed_at IS NOT NULL AND closed_by_user_id IS NOT NULL AND close_reason IS NOT NULL)");
        });

        // Driver/vehicle overlap exclusion constraints (BR-05, BR-06) are added by raw SQL in the
        // InitialCreate migration - see DbConstraints.

        builder.Property(a => a.Version).IsRowVersion();
    }
}
