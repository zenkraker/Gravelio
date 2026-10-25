using Gravelio.Api.Activities;
using Gravelio.Api.Data;
using Gravelio.Api.Privacy;
using Gravelio.Api.Users;

namespace Gravelio.Tests;

public class PrivacyMaskTests
{
    static readonly DateTime Start = new(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc);

    static ActivityRead Activity() => new(1, "Ride", "desc", 1000, 4, Start, Start.AddHours(1), "UTC", 3600, 3500, 100, 90, 0.5,
        3.0, 5.0, 200, 400, 250, 140, 170, 85, 95, 300, Visibilities.Public, "Garmin", "Edge", Start, 7,
        new PersonRead(42, "Owner", "owner", null));

    static readonly UserPrivacySettings Hidden = new()
    {
        HideActivityStartTime = true,
        HideActivityLocation = true,
        HideActivityMap = true,
        HideActivityHr = true,
        HideActivityPower = true,
        HideActivityCadence = true,
        HideActivityElevation = true,
        HideActivitySpeed = true,
        HideActivityPace = true,
        HideActivityGear = true,
    };

    [Fact]
    public void Non_owners_get_hidden_fields_blanked_and_owners_keep_everything()
    {
        var activity = Activity();
        var masked = PrivacyMask.Apply(activity, Hidden, false);

        Assert.Null(masked.StartTime);
        Assert.Null(masked.EndTime);
        Assert.Null(masked.GearId);
        Assert.Null(masked.AverageHr);
        Assert.Null(masked.MaxHr);
        Assert.Null(masked.AveragePower);
        Assert.Null(masked.MaxPower);
        Assert.Null(masked.NormalizedPower);
        Assert.Null(masked.AverageCad);
        Assert.Null(masked.MaxCad);
        Assert.Null(masked.AverageSpeed);
        Assert.Null(masked.MaxSpeed);
        Assert.Null(masked.Pace);
        Assert.Null(masked.ElevationGain);
        Assert.Null(masked.ElevationLoss);
        // Fields without a hide flag stay visible.
        Assert.Equal("Ride", masked.Name);
        Assert.Equal(1000, masked.Distance);
        Assert.Equal(300, masked.Calories);

        Assert.Same(activity, PrivacyMask.Apply(activity, Hidden, true));
    }

    [Fact]
    public void Streams_follow_their_flag_and_unknown_types_are_default_denied()
    {
        var settings = new UserPrivacySettings { HideActivityHr = true, HideActivityMap = true };
        Assert.False(PrivacyMask.IsStreamVisible(StreamTypes.Hr, settings, false));
        Assert.True(PrivacyMask.IsStreamVisible(StreamTypes.Power, settings, false));
        Assert.False(PrivacyMask.IsStreamVisible(StreamTypes.Map, settings, false));
        // No setting exists for temperature, so it must never default to visible.
        Assert.False(PrivacyMask.IsStreamVisible(StreamTypes.Temperature, settings, false));
        Assert.False(PrivacyMask.IsStreamVisible(99, settings, false));

        var location = new UserPrivacySettings { HideActivityLocation = true };
        Assert.False(PrivacyMask.IsStreamVisible(StreamTypes.Map, location, false));

        Assert.True(PrivacyMask.IsStreamVisible(StreamTypes.Temperature, settings, true));
        Assert.True(PrivacyMask.IsStreamVisible(StreamTypes.Hr, null, false));
        // Default-deny holds even when the user has never saved privacy settings.
        Assert.False(PrivacyMask.IsStreamVisible(StreamTypes.Temperature, null, false));
    }

    [Fact]
    public void Relative_waypoints_keep_spacing_without_exposing_the_real_date()
    {
        const string json = """[{"time":"2026-05-01T08:00:10Z","hr":120},{"time":"2026-05-01T08:00:20Z","hr":121}]""";
        var masked = PrivacyMask.RelativeWaypoints(json, Start);

        Assert.DoesNotContain("2026-05-01", masked.GetRawText());
        var times = masked.EnumerateArray().Select(w => w.GetProperty("time").GetString()).ToList();
        Assert.Equal(["1970-01-01T00:00:10Z", "1970-01-01T00:00:20Z"], times);
        Assert.Equal(120, masked[0].GetProperty("hr").GetInt32());
    }
}
