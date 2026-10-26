using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Data;

public class User : IdentityUser<int>
{
    public required string Name { get; set; }
    public bool Active { get; set; } = true;

    /// Signed up while an admin has to approve new accounts; stays inactive until then, which is different from a deactivated account.
    public bool PendingApproval { get; set; }

    public string? City { get; set; }
    public DateOnly? Birthdate { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public string Gender { get; set; } = "unspecified";
    public string Units { get; set; } = "metric";
    public int? Height { get; set; }
    public int? MaxHeartRate { get; set; }
    public string FirstDayOfWeek { get; set; } = "monday";
    public string Timezone { get; set; } = "UTC";
    public string Currency { get; set; } = "euro";
    public DateTime CreatedAt { get; set; }
}

public class Activity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int Distance { get; set; }
    public int ActivityType { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Timezone { get; set; }
    public double TotalElapsedTime { get; set; }
    public double TotalTimerTime { get; set; }
    public int? ElevationGain { get; set; }
    public int? ElevationLoss { get; set; }
    public double? Pace { get; set; }
    public double? AverageSpeed { get; set; }
    public double? MaxSpeed { get; set; }
    public int? AveragePower { get; set; }
    public int? MaxPower { get; set; }
    public int? NormalizedPower { get; set; }
    public int? AverageHr { get; set; }
    public int? MaxHr { get; set; }
    public int? AverageCad { get; set; }
    public int? MaxCad { get; set; }
    public int? Calories { get; set; }
    public int Visibility { get; set; } = Visibilities.Private;
    public string? TrackerManufacturer { get; set; }
    public string? TrackerModel { get; set; }
    public int? GearId { get; set; }
    public DateTime CreatedAt { get; set; }
    /// Notes only the owner can read; no response for any other viewer includes them.
    public string? PrivateNotes { get; set; }
    /// How the effort felt, 0-100.
    public int? WorkoutFeeling { get; set; }
    /// Rate of perceived exertion, 10-100.
    public int? WorkoutRpe { get; set; }
    /// What the owner hides on this activity, replacing their privacy settings; null means use the settings.
    public Privacy.HiddenDetails? HiddenDetails { get; set; }
    /// Shown to the owner only, and left out of statistics, goals, gear totals, feeds and public links. Set on an upload that looks like a
    /// duplicate of another recording, and by the owner.
    public bool IsHidden { get; set; }
    /// Identifies a recording by its file and start, so uploading the same file again is recognised. Null on activities that predate it.
    public string? DedupKey { get; set; }
    public List<ActivityStream> Streams { get; set; } = [];
    public List<ActivityLap> Laps { get; set; } = [];
}

public static class Visibilities
{
    public const int Public = 0;
    public const int Followers = 1;
    public const int Private = 2;
}

public class ActivityStream
{
    public int Id { get; set; }
    public int ActivityId { get; set; }
    public int StreamType { get; set; }
    // Raw JSON array, e.g. [{"time":"...","hr":120}] or [{"time":"...","lat":..,"lon":..}]
    public required string Waypoints { get; set; }
}

/// One lap of a recording as the device or file reported it. Coordinates are private like the map: see <c>VisibleLaps</c>.
public class ActivityLap
{
    public int Id { get; set; }
    public int ActivityId { get; set; }
    public int Index { get; set; }
    public DateTime StartTime { get; set; }
    public double? StartLat { get; set; }
    public double? StartLon { get; set; }
    public double? EndLat { get; set; }
    public double? EndLon { get; set; }
    public double TotalElapsedTime { get; set; }
    public double? TotalTimerTime { get; set; }
    public double? Distance { get; set; }
    public int? AverageHr { get; set; }
    public int? MaxHr { get; set; }
    public int? AverageCad { get; set; }
    public int? MaxCad { get; set; }
    public int? AveragePower { get; set; }
    public int? MaxPower { get; set; }
    public int? ElevationGain { get; set; }
    public int? ElevationLoss { get; set; }
    public double? AverageSpeed { get; set; }
    public double? MaxSpeed { get; set; }
    public int? Calories { get; set; }
}

public static class StreamTypes
{
    public const int Hr = 1, Power = 2, Cadence = 3, Elevation = 4, Speed = 5, Pace = 6, Map = 7, Temperature = 8;
}

public class Gear
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Nickname { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public int GearType { get; set; }
    public bool Active { get; set; } = true;
    /// Meters already covered before the gear was added here.
    public double InitialDistance { get; set; }
    public decimal? PurchaseValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<GearComponent> Components { get; set; } = [];
}

public class GearComponent
{
    public int Id { get; set; }
    public int GearId { get; set; }
    public Gear Gear { get; set; } = null!;
    public required string Type { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public DateOnly PurchaseDate { get; set; }
    public DateOnly? RetiredDate { get; set; }
    public bool Active { get; set; } = true;
    /// Wear threshold in meters; the UI compares it with the gear's distance.
    public double? ExpectedDistance { get; set; }
    public decimal? PurchaseValue { get; set; }
}

/// The gear a user wants pre-selected for imported activities of one type.
public class DefaultGear
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ActivityType { get; set; }
    public int GearId { get; set; }
    public Gear Gear { get; set; } = null!;
}

/// A follow request, pending until the followee accepts it.
public class Follow
{
    public int Id { get; set; }
    public int FollowerId { get; set; }
    public User Follower { get; set; } = null!;
    public int FolloweeId { get; set; }
    public User Followee { get; set; } = null!;
    public required string Status { get; set; } = FollowStatuses.Pending;
    public DateTime CreatedAt { get; set; }
}

public static class FollowStatuses
{
    public const string Pending = "pending", Accepted = "accepted";
}

/// A message for one user about something that happened elsewhere. The actor's name is a snapshot, so the row survives
/// that account being deleted and needs no join to render.
public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int Type { get; set; }
    public int ActorId { get; set; }
    public required string ActorName { get; set; }
    /// The activity a notification is about, when it is about one. Not a foreign key: the notification outlives the activity.
    public int? ActivityId { get; set; }
    public string? ActivityName { get; set; }
    public bool Read { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// The hash of a password-reset link's token; the raw token only exists in the email.
public class PasswordResetToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public required string TokenHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// A target for one kind of activity over one period, e.g. "100 km of bike per month". The unique index allows one target per
/// combination of period, activity group and metric. Distance is meters, duration seconds, elevation meters, calories kcal.
public class Goal
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Interval { get; set; }
    public required string ActivityGroup { get; set; }
    public required string Metric { get; set; }
    public int Target { get; set; }
}

/// A key for programs that upload activities for a user without a sign-in (a watch app, a sync script). Only the SHA-256 hash is
/// stored, so a leaked database cannot be used to upload; the key itself is shown once, when it is made.
/// A signed-in browser or app. The auth cookie carries only <see cref="Key"/>; the ticket itself stays here, so deleting the row signs that device out.
public class UserSession
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Key { get; set; }
    public required byte[] Ticket { get; set; }
    public required string Device { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class ApiKey
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Name { get; set; }
    /// The first characters of the key, so a person can tell their keys apart.
    public required string Prefix { get; set; }
    public required string KeyHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

/// An authenticator code that has already signed someone in or confirmed a change. A code stays valid for a couple of minutes,
/// and RFC 6238 §5.2 forbids accepting it twice; the unique index also rejects two parallel submissions.
public class UsedTotpCode
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Code { get; set; }
    public DateTime UsedAt { get; set; }
}

/// Instance-wide settings. Exactly one row exists once anything has been saved; a missing row means all defaults.
public class ServerSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public bool SignupEnabled { get; set; }
    public bool SignupRequireEmailVerification { get; set; }
    public bool SignupRequireAdminApproval { get; set; }
    /// Anyone with the link may open a public activity. Off by default: a server only publishes once its admin says so.
    public bool PublicShareableLinks { get; set; }
    /// A public link also shows the owner's name.
    public bool PublicShareableLinksUserInfo { get; set; }
}

/// Per-user activity privacy. Owners always see everything; these flags mask other viewers.
public class UserPrivacySettings
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int DefaultActivityVisibility { get; set; } = Visibilities.Private;
    public bool HideActivityStartTime { get; set; }
    public bool HideActivityLocation { get; set; }
    public bool HideActivityMap { get; set; }
    public bool HideActivityHr { get; set; }
    public bool HideActivityPower { get; set; }
    public bool HideActivityCadence { get; set; }
    public bool HideActivityElevation { get; set; }
    public bool HideActivitySpeed { get; set; }
    public bool HideActivityPace { get; set; }
    public bool HideActivityGear { get; set; }
}

/// One body-composition reading per user and day.
public class HealthWeight
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateOnly Date { get; set; }
    /// Kilograms.
    public decimal Weight { get; set; }
    public decimal? Bmi { get; set; }
    public decimal? BodyFat { get; set; }
    public decimal? BodyWater { get; set; }
    public decimal? BoneMass { get; set; }
    public decimal? MuscleMass { get; set; }
    public int? PhysiqueRating { get; set; }
    public decimal? VisceralFat { get; set; }
    public int? MetabolicAge { get; set; }
    /// Set by integrations (e.g. "garmin"); null for manual entries.
    public string? Source { get; set; }
}

/// One daily step total per user; a second save for the same day replaces it.
public class HealthStep
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateOnly Date { get; set; }
    public int Steps { get; set; }
    /// Set by integrations (e.g. "garmin"); null for manual entries.
    public string? Source { get; set; }
}

/// One sleep session per user and day, with the metrics a device reports and scores computed here.
public class HealthSleep
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateOnly Date { get; set; }
    public DateTime? SleepStartTimeGmt { get; set; }
    public DateTime? SleepEndTimeGmt { get; set; }
    public DateTime? SleepStartTimeLocal { get; set; }
    public DateTime? SleepEndTimeLocal { get; set; }
    public int? TotalSleepSeconds { get; set; }
    public int? NapTimeSeconds { get; set; }
    public int? UnmeasurableSleepSeconds { get; set; }
    public int? DeepSleepSeconds { get; set; }
    public int? LightSleepSeconds { get; set; }
    public int? RemSleepSeconds { get; set; }
    public int? AwakeSleepSeconds { get; set; }
    public int? AvgHeartRate { get; set; }
    public int? MinHeartRate { get; set; }
    public int? MaxHeartRate { get; set; }
    public int? AvgSpo2 { get; set; }
    public int? LowestSpo2 { get; set; }
    public int? HighestSpo2 { get; set; }
    public int? AvgRespiration { get; set; }
    public int? LowestRespiration { get; set; }
    public int? HighestRespiration { get; set; }
    public int? AvgStressLevel { get; set; }
    public int? AwakeCount { get; set; }
    public int? RestlessMomentsCount { get; set; }
    public int? SleepScoreOverall { get; set; }
    public string? SleepScoreDuration { get; set; }
    public string? SleepScoreQuality { get; set; }
    /// Set by integrations (e.g. "garmin"); null for manual entries.
    public string? GarminconnectSleepId { get; set; }
    /// Sleep stage intervals as a JSON array.
    public string? SleepStages { get; set; }
    /// Set by integrations (e.g. "garmin"); null for manual entries.
    public string? Source { get; set; }
    public string? HrvStatus { get; set; }
    public int? RestingHeartRate { get; set; }
    public decimal? AvgSkinTempDeviation { get; set; }
    public string? AwakeCountScore { get; set; }
    public string? RemPercentageScore { get; set; }
    public string? DeepPercentageScore { get; set; }
    public string? LightPercentageScore { get; set; }
    public int? AvgSleepStress { get; set; }
    public string? SleepStressScore { get; set; }
}

/// One daily water total per user; posts add to the day's amount.
public class HealthWater
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateOnly Date { get; set; }
    public decimal AmountMl { get; set; }
    /// Set by integrations (e.g. "garmin"); null for manual entries.
    public string? Source { get; set; }
}

/// One row of health goals per user.
public class HealthTarget
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal? Weight { get; set; }
    public int? Steps { get; set; }
    public int? Sleep { get; set; }
    public int? Fasting { get; set; }
    public decimal? WaterMl { get; set; }
    public int? PoopCount { get; set; }
}

/// A fasting session; exactly one may be in progress per user at a time.
public class HealthFasting
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime FastStartTime { get; set; }
    public DateTime? FastEndTime { get; set; }
    public int? TargetDurationSeconds { get; set; }
    public int? ActualDurationSeconds { get; set; }
    public string? FastingType { get; set; }
    public string Status { get; set; } = FastingStatuses.InProgress;
    public string? Notes { get; set; }
    /// Set by integrations (e.g. "garmin"); "manual" for user entries.
    public string? Source { get; set; }
}

public static class FastingStatuses
{
    public const string InProgress = "in_progress", Completed = "completed", Broken = "broken", Cancelled = "cancelled";
}

public static class FastingTypes
{
    public static readonly string[] All = ["16:8", "18:6", "20:4", "OMAD", "24h", "36h", "48h", "72h", "custom"];
}

/// One bowel-movement record; several per day are allowed.
public class HealthPoop
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime DateTime { get; set; }
    public int? BristolType { get; set; }
    public string? Color { get; set; }
    public string? Notes { get; set; }
    /// Set by integrations (e.g. "manual"); null for manual entries.
    public string? Source { get; set; }
}

public static class PoopColors
{
    public static readonly string[] All = ["brown", "dark_brown", "light_brown", "yellow", "green", "black", "red", "white"];
}

public class AppDb(DbContextOptions<AppDb> options) : IdentityDbContext<User, IdentityRole<int>, int>(options)
{
    /// The wall-clock time in an IANA zone for a UTC instant (PostgreSQL's timezone()), so a query can compare an activity's local date.
    [DbFunction("timezone", IsBuiltIn = true)]
    public static DateTime LocalTime(string zone, DateTime utc) => throw new NotSupportedException("Only translated to SQL.");

    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityStream> ActivityStreams => Set<ActivityStream>();
    public DbSet<ActivityLap> ActivityLaps => Set<ActivityLap>();
    public DbSet<Gear> Gears => Set<Gear>();
    public DbSet<GearComponent> GearComponents => Set<GearComponent>();
    public DbSet<DefaultGear> DefaultGears => Set<DefaultGear>();
    public DbSet<HealthWeight> HealthWeights => Set<HealthWeight>();
    public DbSet<HealthStep> HealthSteps => Set<HealthStep>();
    public DbSet<HealthSleep> HealthSleeps => Set<HealthSleep>();
    public DbSet<HealthWater> HealthWaters => Set<HealthWater>();
    public DbSet<HealthTarget> HealthTargets => Set<HealthTarget>();
    public DbSet<HealthFasting> HealthFastings => Set<HealthFasting>();
    public DbSet<HealthPoop> HealthPoops => Set<HealthPoop>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<UsedTotpCode> UsedTotpCodes => Set<UsedTotpCode>();
    public DbSet<ServerSettings> ServerSettings => Set<ServerSettings>();
    public DbSet<UserPrivacySettings> UserPrivacySettings => Set<UserPrivacySettings>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.HasDbFunction(typeof(AppDb).GetMethod(nameof(LocalTime))!).HasStoreType("timestamp without time zone");
        b.Entity<Activity>().HasOne(a => a.User).WithMany().OnDelete(DeleteBehavior.Cascade);
        b.Entity<Activity>().HasIndex(a => new { a.UserId, a.StartTime });
        // The same recording is stored once; the upload endpoint relies on this to reject parallel duplicates.
        b.Entity<Activity>().HasIndex(a => new { a.UserId, a.DedupKey }).IsUnique().HasFilter("\"DedupKey\" IS NOT NULL");
        b.Entity<Activity>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Activities_WorkoutFeeling", "\"WorkoutFeeling\" BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_Activities_WorkoutRpe", "\"WorkoutRpe\" BETWEEN 10 AND 100");
        });
        b.Entity<ActivityStream>().Property(s => s.Waypoints).HasColumnType("jsonb");
        b.Entity<ActivityStream>().HasIndex(s => new { s.ActivityId, s.StreamType }).IsUnique();
        b.Entity<Gear>().HasOne(g => g.User).WithMany().OnDelete(DeleteBehavior.Cascade);
        b.Entity<Gear>().HasIndex(g => new { g.UserId, g.Nickname });
        b.Entity<Gear>().Property(g => g.PurchaseValue).HasPrecision(12, 2);
        b.Entity<GearComponent>().HasOne(c => c.Gear).WithMany(g => g.Components).HasForeignKey(c => c.GearId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<GearComponent>().Property(c => c.PurchaseValue).HasPrecision(12, 2);
        // Deleting gear keeps the activities and just unlinks them.
        b.Entity<Activity>().HasOne<Gear>().WithMany().HasForeignKey(a => a.GearId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<Activity>().HasIndex(a => a.GearId);
        b.Entity<DefaultGear>().HasOne(d => d.Gear).WithMany().HasForeignKey(d => d.GearId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<DefaultGear>().HasOne<User>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<DefaultGear>().HasIndex(d => new { d.UserId, d.ActivityType }).IsUnique();
        b.Entity<HealthWeight>().HasOne<User>().WithMany().HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Cascade);
        // One reading per day; the create endpoint upserts on it and relies on the index to reject parallel duplicates.
        b.Entity<HealthWeight>().HasIndex(w => new { w.UserId, w.Date }).IsUnique();
        foreach (var p in new[] { nameof(HealthWeight.Weight), nameof(HealthWeight.Bmi), nameof(HealthWeight.BodyFat), nameof(HealthWeight.BodyWater), nameof(HealthWeight.BoneMass), nameof(HealthWeight.MuscleMass), nameof(HealthWeight.VisceralFat) })
            b.Entity<HealthWeight>().Property(p).HasPrecision(10, 2);
        b.Entity<HealthStep>().HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<HealthStep>().HasIndex(s => new { s.UserId, s.Date }).IsUnique();
        b.Entity<HealthSleep>().HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<HealthSleep>().HasIndex(s => new { s.UserId, s.Date }).IsUnique();
        b.Entity<HealthSleep>().Property(s => s.SleepStages).HasColumnType("jsonb");
        b.Entity<HealthSleep>().Property(s => s.AvgSkinTempDeviation).HasPrecision(10, 2);
        b.Entity<HealthWater>().HasOne<User>().WithMany().HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<HealthWater>().HasIndex(w => new { w.UserId, w.Date }).IsUnique();
        b.Entity<HealthWater>().Property(w => w.AmountMl).HasPrecision(10, 2);
        b.Entity<HealthTarget>().HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<HealthTarget>().HasIndex(t => t.UserId).IsUnique();
        b.Entity<HealthTarget>().Property(t => t.Weight).HasPrecision(10, 2);
        b.Entity<HealthTarget>().Property(t => t.WaterMl).HasPrecision(10, 2);
        b.Entity<HealthFasting>().HasOne<User>().WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<HealthFasting>().HasIndex(f => new { f.UserId, f.FastStartTime });
        // At most one in-progress session per user, enforced by the database for parallel starts.
        b.Entity<HealthFasting>().HasIndex(f => f.UserId).IsUnique().HasFilter("\"Status\" = 'in_progress'");
        b.Entity<HealthFasting>().Property(f => f.Status).HasMaxLength(20);
        b.Entity<HealthFasting>().ToTable(t =>
            t.HasCheckConstraint("CK_HealthFastings_Status", "\"Status\" IN ('in_progress', 'completed', 'broken', 'cancelled')"));
        b.Entity<HealthPoop>().HasOne<User>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<HealthPoop>().HasIndex(p => new { p.UserId, p.DateTime });
        b.Entity<Follow>().HasOne(f => f.Follower).WithMany().HasForeignKey(f => f.FollowerId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Follow>().HasOne(f => f.Followee).WithMany().HasForeignKey(f => f.FolloweeId).OnDelete(DeleteBehavior.Cascade);
        // One relationship per direction; the follow endpoint relies on this to reject parallel requests.
        b.Entity<Follow>().HasIndex(f => new { f.FollowerId, f.FolloweeId }).IsUnique();
        b.Entity<Follow>().HasIndex(f => f.FolloweeId);
        b.Entity<Follow>().Property(f => f.Status).HasMaxLength(20);
        b.Entity<Notification>().HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Notification>().HasIndex(n => new { n.UserId, n.CreatedAt });
        b.Entity<Notification>().HasIndex(n => new { n.UserId, n.Read });
        b.Entity<PasswordResetToken>().HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<PasswordResetToken>().HasIndex(t => t.TokenHash).IsUnique();
        b.Entity<PasswordResetToken>().HasIndex(t => t.UserId);
        // PostgreSQL's per-row version: the ETag of an activity or gear, and what makes a stale save fail instead of overwrite.
        b.Entity<Activity>().Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        b.Entity<Gear>().Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        b.Entity<ApiKey>().HasOne(k => k.User).WithMany().HasForeignKey(k => k.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ApiKey>().HasIndex(k => k.KeyHash).IsUnique();
        b.Entity<ApiKey>().HasIndex(k => k.UserId);
        b.Entity<UserSession>().HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<UserSession>().HasIndex(s => s.Key).IsUnique();
        b.Entity<UserSession>().HasIndex(s => s.UserId);
        b.Entity<UserSession>().HasIndex(s => s.ExpiresAt);
        b.Entity<UserSession>().Property(s => s.Key).HasMaxLength(64);
        b.Entity<UserSession>().Property(s => s.Device).HasMaxLength(100);
        b.Entity<UserSession>().Property(s => s.IpAddress).HasMaxLength(45);
        b.Entity<Goal>().HasOne<User>().WithMany().HasForeignKey(g => g.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Goal>().HasIndex(g => new { g.UserId, g.Interval, g.ActivityGroup, g.Metric }).IsUnique();
        b.Entity<Goal>().Property(g => g.Interval).HasMaxLength(20);
        b.Entity<Goal>().Property(g => g.ActivityGroup).HasMaxLength(20);
        b.Entity<Goal>().Property(g => g.Metric).HasMaxLength(20);
        b.Entity<Goal>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Goals_Interval", "\"Interval\" IN ('daily', 'weekly', 'monthly', 'yearly')");
            t.HasCheckConstraint("CK_Goals_ActivityGroup", "\"ActivityGroup\" IN ('run', 'bike', 'swim', 'walk', 'strength', 'cardio')");
            t.HasCheckConstraint("CK_Goals_Metric", "\"Metric\" IN ('calories', 'activities', 'distance', 'elevation', 'duration')");
            t.HasCheckConstraint("CK_Goals_Target", "\"Target\" > 0");
        });
        b.Entity<UsedTotpCode>().HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<UsedTotpCode>().HasIndex(c => new { c.UserId, c.Code }).IsUnique();
        b.Entity<UsedTotpCode>().HasIndex(c => c.UsedAt);
        // A hand-managed singleton row: the app assigns Id 1 rather than the database.
        b.Entity<ServerSettings>().Property(s => s.Id).ValueGeneratedNever();
        b.Entity<Follow>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Follows_NotSelf", "\"FollowerId\" <> \"FolloweeId\"");
            t.HasCheckConstraint("CK_Follows_Status", "\"Status\" IN ('pending', 'accepted')");
        });
        b.Entity<UserPrivacySettings>().HasOne(p => p.User).WithOne().HasForeignKey<UserPrivacySettings>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<UserPrivacySettings>().HasIndex(p => p.UserId).IsUnique();
        b.Entity<UserPrivacySettings>().ToTable(t =>
            t.HasCheckConstraint("CK_UserPrivacySettings_Visibility", "\"DefaultActivityVisibility\" BETWEEN 0 AND 2"));
        b.Entity<Activity>().HasMany(a => a.Streams).WithOne().HasForeignKey(s => s.ActivityId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Activity>().HasMany(a => a.Laps).WithOne().HasForeignKey(l => l.ActivityId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ActivityLap>().HasIndex(l => new { l.ActivityId, l.Index }).IsUnique();
    }
}
