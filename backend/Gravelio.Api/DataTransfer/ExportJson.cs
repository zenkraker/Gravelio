using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Gravelio.Api.Data;

namespace Gravelio.Api.DataTransfer;

/// How a person's data is written to and read from an export file. The entities are serialized as they are, so a new column is
/// exported automatically and an old export still imports; the owner and the links back to the owner are left out, because an
/// import always files everything under the account that imports it.
public static class ExportJson
{
    public const string Format = "gravelio-export";
    public const int Version = 1;

    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        MaxDepth = 16,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { DropOwnership } },
    };

    static void DropOwnership(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object) return;
        for (var i = info.Properties.Count - 1; i >= 0; i--)
        {
            var p = info.Properties[i];
            if (p.Name == "user_id" || p.PropertyType == typeof(User) || p.PropertyType == typeof(Gear)) info.Properties.RemoveAt(i);
        }
    }
}

public sealed record ExportManifest(string Format, int Version, DateTime ExportedAt);

/// The account's own preferences. Kept for the person's records; an import never overwrites a profile.
public sealed record ExportProfile(string Name, string Email, string? City, DateOnly? Birthdate, string PreferredLanguage, string Gender, string Units,
    int? Height, int? MaxHeartRate, string FirstDayOfWeek, string Timezone, string Currency);

/// Everything except the activities, which are one file each so a large history never has to be in memory at once.
public sealed record ExportedData(ExportProfile Profile, UserPrivacySettings? Privacy, List<Gear> Gears, List<DefaultGear> DefaultGears, List<Goal> Goals,
    List<HealthWeight> Weight, List<HealthStep> Steps, List<HealthSleep> Sleep, List<HealthWater> Water, HealthTarget? Targets,
    List<HealthFasting> Fasting, List<HealthPoop> Poop);
