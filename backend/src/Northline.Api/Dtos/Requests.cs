using System.ComponentModel.DataAnnotations;

namespace Northline.Api.Dtos;

public class LoginRequest
{
    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Password { get; set; } = string.Empty;
}

public class CreateAssignmentRequest
{
    [Required, Range(1, int.MaxValue)]
    public int? OrderId { get; set; }

    [Required, Range(1, int.MaxValue)]
    public int? DriverId { get; set; }

    [Required, Range(1, int.MaxValue)]
    public int? VehicleId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class ReassignAssignmentRequest
{
    /// <summary>The assignment's current version, as returned by GET. Detects stale updates.</summary>
    [Required]
    public uint? Version { get; set; }

    /// <summary>New driver. Omit to keep the current driver.</summary>
    [Range(1, int.MaxValue)]
    public int? DriverId { get; set; }

    /// <summary>New vehicle. Omit to keep the current vehicle.</summary>
    [Range(1, int.MaxValue)]
    public int? VehicleId { get; set; }

    [Required, MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class CancelAssignmentRequest
{
    [Required]
    public uint? Version { get; set; }

    [Required, MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class CompleteAssignmentRequest
{
    [Required]
    public uint? Version { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }
}

public enum SortDirection
{
    Asc,
    Desc
}

public class PageQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 25;

    [MaxLength(100)]
    public string? Search { get; set; }

    public SortDirection Dir { get; set; } = SortDirection.Asc;
}
