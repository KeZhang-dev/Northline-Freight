namespace Northline.Api.Data.Seed;

public class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>
    /// Password given to every seeded development account. Development only; supplied via configuration
    /// (appsettings.Development.json or the SEED__DEVPASSWORD environment variable). Seeding fails if empty.
    /// </summary>
    public string DevPassword { get; set; } = string.Empty;
}

public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Apply pending EF Core migrations on startup.</summary>
    public bool MigrateOnStartup { get; set; } = true;

    /// <summary>Seed the development dataset on startup if the database is empty.</summary>
    public bool SeedOnStartup { get; set; } = true;
}
