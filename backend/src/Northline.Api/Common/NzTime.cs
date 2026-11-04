using System.Globalization;
using Northline.Api.Entities;

namespace Northline.Api.Common;

/// <summary>All timestamps are stored in UTC. Messages and seed data use New Zealand local time.</summary>
public static class NzTime
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");

    /// <summary>Fixed culture so messages do not depend on the server's locale.</summary>
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-NZ");

    public static DateTimeOffset ToLocal(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, Zone);

    public static string Format(DateTimeOffset utc) => ToLocal(utc).ToString("ddd d MMM yyyy HH:mm", Culture);

    public static string FormatRange(DateTimeOffset start, DateTimeOffset end) => $"{Format(start)} – {Format(end)}";

    /// <summary>Converts an Auckland local date/time to a UTC DateTimeOffset.</summary>
    public static DateTimeOffset FromLocal(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, Zone);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    public static DateTime LocalToday(DateTimeOffset nowUtc) => ToLocal(nowUtc).Date;
}

public static class DisplayNames
{
    public static string Kg(decimal value) => value.ToString("#,0.##", CultureInfo.InvariantCulture) + " kg";

    public static string Display(this VehicleType type) => type switch
    {
        VehicleType.LightCommercial => "Light Commercial Vehicle",
        VehicleType.MediumRigid => "Medium Rigid Truck",
        VehicleType.HeavyRigid => "Heavy Rigid Truck",
        VehicleType.HeavyCombination => "Heavy Combination Vehicle",
        _ => type.ToString(),
    };
}
