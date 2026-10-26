using Gravelio.Api.Data;

namespace Gravelio.Api.Privacy;

/// The details of one activity its owner can hide from other viewers. Stored on the activity as a bit set; no value at all means
/// "use my privacy settings", while a value (even none) replaces them for that activity.
[Flags]
public enum HiddenDetails
{
    None = 0,
    StartTime = 1,
    Location = 2,
    Map = 4,
    Hr = 8,
    Power = 16,
    Cadence = 32,
    Elevation = 64,
    Speed = 128,
    Pace = 256,
    Gear = 512,
}

public static class HiddenDetailNames
{
    /// The names on the wire, the same ones the privacy settings use.
    public static readonly IReadOnlyDictionary<string, HiddenDetails> ByName = new Dictionary<string, HiddenDetails>
    {
        ["start_time"] = HiddenDetails.StartTime,
        ["location"] = HiddenDetails.Location,
        ["map"] = HiddenDetails.Map,
        ["hr"] = HiddenDetails.Hr,
        ["power"] = HiddenDetails.Power,
        ["cadence"] = HiddenDetails.Cadence,
        ["elevation"] = HiddenDetails.Elevation,
        ["speed"] = HiddenDetails.Speed,
        ["pace"] = HiddenDetails.Pace,
        ["gear"] = HiddenDetails.Gear,
    };

    public static string[]? ToNames(HiddenDetails? details) =>
        details is null ? null : ByName.Where(n => details.Value.HasFlag(n.Value)).Select(n => n.Key).ToArray();

    public static HiddenDetails? FromNames(IEnumerable<string>? names) =>
        names?.Aggregate(HiddenDetails.None, (all, name) => all | ByName[name]);

    /// The settings that apply to one activity: its own choice when it has one, otherwise the owner's.
    public static UserPrivacySettings? Effective(this UserPrivacySettings? settings, HiddenDetails? activityChoice) =>
        activityChoice is not { } choice ? settings : new UserPrivacySettings
        {
            UserId = settings?.UserId ?? 0,
            DefaultActivityVisibility = settings?.DefaultActivityVisibility ?? Visibilities.Private,
            HideActivityStartTime = choice.HasFlag(HiddenDetails.StartTime),
            HideActivityLocation = choice.HasFlag(HiddenDetails.Location),
            HideActivityMap = choice.HasFlag(HiddenDetails.Map),
            HideActivityHr = choice.HasFlag(HiddenDetails.Hr),
            HideActivityPower = choice.HasFlag(HiddenDetails.Power),
            HideActivityCadence = choice.HasFlag(HiddenDetails.Cadence),
            HideActivityElevation = choice.HasFlag(HiddenDetails.Elevation),
            HideActivitySpeed = choice.HasFlag(HiddenDetails.Speed),
            HideActivityPace = choice.HasFlag(HiddenDetails.Pace),
            HideActivityGear = choice.HasFlag(HiddenDetails.Gear),
        };
}
