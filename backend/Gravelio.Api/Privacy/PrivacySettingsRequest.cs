using Gravelio.Api.Data;

namespace Gravelio.Api.Privacy;

public sealed record PrivacySettingsRequest(int DefaultActivityVisibility = Visibilities.Private, bool HideActivityStartTime = false,
    bool HideActivityLocation = false, bool HideActivityMap = false, bool HideActivityHr = false, bool HideActivityPower = false,
    bool HideActivityCadence = false, bool HideActivityElevation = false, bool HideActivitySpeed = false, bool HideActivityPace = false,
    bool HideActivityGear = false, bool HideActivityLaps = false);

public static class PrivacySettingsExtensions
{
    public static void Apply(this UserPrivacySettings s, PrivacySettingsRequest r) =>
        (s.DefaultActivityVisibility, s.HideActivityStartTime, s.HideActivityLocation, s.HideActivityMap, s.HideActivityHr, s.HideActivityPower,
            s.HideActivityCadence, s.HideActivityElevation, s.HideActivitySpeed, s.HideActivityPace, s.HideActivityGear, s.HideActivityLaps) =
        (r.DefaultActivityVisibility, r.HideActivityStartTime, r.HideActivityLocation, r.HideActivityMap, r.HideActivityHr, r.HideActivityPower,
            r.HideActivityCadence, r.HideActivityElevation, r.HideActivitySpeed, r.HideActivityPace, r.HideActivityGear, r.HideActivityLaps);
}
