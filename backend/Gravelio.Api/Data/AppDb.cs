using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Data;

public class User
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public string AccessType { get; set; } = AccessTypes.Regular;
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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class AccessTypes
{
    public const string Regular = "regular";
    public const string Admin = "admin";
}

public class UserSession
{
    public required string Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public required string RefreshTokenHash { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public int RotationCount { get; set; }
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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
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

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> Sessions => Set<UserSession>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityStream> ActivityStreams => Set<ActivityStream>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Username).IsUnique();
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();
        b.Entity<UserSession>().HasOne(s => s.User).WithMany().OnDelete(DeleteBehavior.Cascade);
        b.Entity<Activity>().HasOne(a => a.User).WithMany().OnDelete(DeleteBehavior.Cascade);
        b.Entity<Activity>().HasIndex(a => new { a.UserId, a.StartTime });
        b.Entity<ActivityStream>().Property(s => s.Waypoints).HasColumnType("jsonb");
        b.Entity<ActivityStream>().HasIndex(s => new { s.ActivityId, s.StreamType }).IsUnique();
        b.Entity<Activity>().HasMany(a => a.Streams).WithOne().HasForeignKey(s => s.ActivityId).OnDelete(DeleteBehavior.Cascade);
    }
}
