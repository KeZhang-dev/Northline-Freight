using Microsoft.EntityFrameworkCore;
using Northline.Api.Data;
using Npgsql;

namespace Northline.Api.Common;

/// <summary>
/// Translates database-level integrity failures into domain errors. These are the last line of
/// defence: they fire when two requests race past the service-level checks at the same time.
/// </summary>
public static class PostgresErrorTranslator
{
    public static DomainException? Translate(Exception exception)
    {
        // EF Core / Npgsql may wrap the database error several levels deep
        // (e.g. InvalidOperationException -> DbUpdateException -> PostgresException).
        var chain = new List<Exception>();
        for (var e = exception; e is not null; e = e.InnerException)
        {
            chain.Add(e);
        }

        if (chain.OfType<DbUpdateConcurrencyException>().Any())
        {
            return DomainException.Conflict(ErrorCodes.AssignmentModified,
                "The record was modified by another user. Reload it and try again.", BusinessRules.Lifecycle);
        }

        var pg = chain.OfType<PostgresException>().FirstOrDefault();
        if (pg is null)
        {
            return null;
        }

        return (pg.SqlState, pg.ConstraintName) switch
        {
            (PostgresErrorCodes.UniqueViolation, DbConstraints.ActiveOrderUnique) =>
                DomainException.Conflict(ErrorCodes.OrderAlreadyAssigned,
                    "This order already has an active assignment.", BusinessRules.DuplicateAssignment),
            (PostgresErrorCodes.UniqueViolation, DbConstraints.PreviousAssignmentUnique) =>
                DomainException.Conflict(ErrorCodes.AssignmentModified,
                    "This assignment has already been reassigned by another request.", BusinessRules.Reassignment),
            (PostgresErrorCodes.ExclusionViolation, DbConstraints.DriverOverlap) =>
                DomainException.Conflict(ErrorCodes.DriverScheduleConflict,
                    "The driver was allocated to an overlapping assignment by another request.", BusinessRules.DriverSchedule),
            (PostgresErrorCodes.ExclusionViolation, DbConstraints.VehicleOverlap) =>
                DomainException.Conflict(ErrorCodes.VehicleScheduleConflict,
                    "The vehicle was allocated to an overlapping assignment by another request.", BusinessRules.VehicleSchedule),
            (PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure, _) =>
                DomainException.Conflict(ErrorCodes.ConcurrencyConflict,
                    "The request conflicted with a concurrent update. Please retry."),
            _ => null
        };
    }
}
