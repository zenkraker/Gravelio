using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Data;

public class User : IdentityUser<int>
{
    public required string Name { get; set; }
    public bool Active { get; set; } = true;
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
    public List<ActivityStream> Streams { get; set; } = [];
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

public class AppDb(DbContextOptions<AppDb> options) : IdentityDbContext<User, IdentityRole<int>, int>(options)
{
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityStream> ActivityStreams => Set<ActivityStream>();
    public DbSet<Gear> Gears => Set<Gear>();
    public DbSet<GearComponent> GearComponents => Set<GearComponent>();
    public DbSet<DefaultGear> DefaultGears => Set<DefaultGear>();
    public DbSet<HealthWeight> HealthWeights => Set<HealthWeight>();
    public DbSet<HealthStep> HealthSteps => Set<HealthStep>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<UserPrivacySettings> UserPrivacySettings => Set<UserPrivacySettings>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Activity>().HasOne(a => a.User).WithMany().OnDelete(DeleteBehavior.Cascade);
        // One recording per user and start time; the upload endpoint relies on this to reject parallel duplicates.
        b.Entity<Activity>().HasIndex(a => new { a.UserId, a.StartTime }).IsUnique();
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
        b.Entity<Follow>().HasOne(f => f.Follower).WithMany().HasForeignKey(f => f.FollowerId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Follow>().HasOne(f => f.Followee).WithMany().HasForeignKey(f => f.FolloweeId).OnDelete(DeleteBehavior.Cascade);
        // One relationship per direction; the follow endpoint relies on this to reject parallel requests.
        b.Entity<Follow>().HasIndex(f => new { f.FollowerId, f.FolloweeId }).IsUnique();
        b.Entity<Follow>().HasIndex(f => f.FolloweeId);
        b.Entity<Follow>().Property(f => f.Status).HasMaxLength(20);
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
    }
}
