using Microsoft.Extensions.Options;
using Northline.Api.Entities;

namespace Northline.Api.Validation;

/// <summary>
/// Configuration bound from the "Dispatch" section of appsettings.json.
/// </summary>
public class DispatchOptions
{
    public const string SectionName = "Dispatch";

    /// <summary>
    /// Minimum simplified licence class (1-5) required for each vehicle type. A higher class covers
    /// all lower classes.
    ///
    /// IMPORTANT: this is a project-specific simplification for a training application. It is NOT a
    /// complete or accurate representation of New Zealand driver licensing regulations
    /// (e.g. learner/restricted stages, endorsements, gross laden mass thresholds are ignored).
    /// </summary>
    public Dictionary<VehicleType, int> MinimumLicenceClass { get; set; } = new()
    {
        [VehicleType.LightCommercial] = 1,
        [VehicleType.MediumRigid] = 2,
        [VehicleType.HeavyRigid] = 4,
        [VehicleType.HeavyCombination] = 5,
    };
}

public class DispatchOptionsValidator : IValidateOptions<DispatchOptions>
{
    public ValidateOptionsResult Validate(string? name, DispatchOptions options)
    {
        var failures = new List<string>();
        foreach (var type in Enum.GetValues<VehicleType>())
        {
            if (!options.MinimumLicenceClass.TryGetValue(type, out var cls))
            {
                failures.Add($"Dispatch:MinimumLicenceClass is missing an entry for {type}.");
            }
            else if (cls is < 1 or > 5)
            {
                failures.Add($"Dispatch:MinimumLicenceClass:{type} must be between 1 and 5 (was {cls}).");
            }
        }
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

/// <summary>BR-07 helper: the simplified, configurable licence compatibility rules.</summary>
public class LicenceRules(IOptions<DispatchOptions> options)
{
    private readonly DispatchOptions _options = options.Value;

    public int MinimumClassFor(VehicleType type) => _options.MinimumLicenceClass[type];

    public bool IsQualified(int licenceClass, VehicleType type) => licenceClass >= MinimumClassFor(type);

    public IReadOnlyList<VehicleType> PermittedVehicleTypes(int licenceClass) =>
        Enum.GetValues<VehicleType>().Where(t => IsQualified(licenceClass, t)).ToList();

    public IReadOnlyDictionary<VehicleType, int> All => _options.MinimumLicenceClass;
}
