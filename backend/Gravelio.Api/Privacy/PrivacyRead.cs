using Gravelio.Api.Data;

namespace Gravelio.Api.Privacy;

public sealed record PrivacyRead(int DefaultActivityVisibility, bool HideActivityStartTime, bool HideActivityLocation, bool HideActivityMap,
    bool HideActivityHr, bool HideActivityPower, bool HideActivityCadence, bool HideActivityElevation, bool HideActivitySpeed,
    bool HideActivityPace, bool HideActivityGear)
{
    public static PrivacyRead From(UserPrivacySettings? s) => s is null
        ? new(Visibilities.Private, false, false, false, false, false, false, false, false, false, false)
        : new(s.DefaultActivityVisibility, s.HideActivityStartTime, s.HideActivityLocation, s.HideActivityMap, s.HideActivityHr,
            s.HideActivityPower, s.HideActivityCadence, s.HideActivityElevation, s.HideActivitySpeed, s.HideActivityPace, s.HideActivityGear);
}
