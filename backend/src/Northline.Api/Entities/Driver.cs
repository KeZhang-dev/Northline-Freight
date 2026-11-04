namespace Northline.Api.Entities;

public class Driver
{
    public int Id { get; set; }
    public string DriverCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>Simplified licence class 1-5 (project-specific rule, see docs/BUSINESS_RULES.md).</summary>
    public int LicenceClass { get; set; }
    public EmploymentStatus EmploymentStatus { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public string FullName => $"{FirstName} {LastName}";

    public List<DispatchAssignment> Assignments { get; set; } = [];
}
