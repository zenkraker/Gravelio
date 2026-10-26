using Gravelio.Api.Data;

namespace Gravelio.Api.Notifications;

public static class NotificationWriter
{
    public static void Add(AppDb db, int userId, int type, int actorId, string actorName, DateTime now) =>
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            ActorId = actorId,
            ActorName = actorName,
            CreatedAt = now,
        });

    /// Someone a person follows recorded an activity they may see.
    public static void AddNewActivity(this AppDb db, int followerId, int ownerId, string ownerName, int activityId, string? activityName, DateTime now) =>
        db.Notifications.Add(new Notification
        {
            UserId = followerId,
            Type = NotificationTypes.NewActivity,
            ActorId = ownerId,
            ActorName = ownerName,
            ActivityId = activityId,
            ActivityName = activityName,
            CreatedAt = now,
        });

    /// A recording that looks like one the user already has was stored hidden; this tells them, with a way to open it.
    public static void AddDuplicate(AppDb db, int userId, string userName, int activityId, string? activityName, DateTime now) =>
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = NotificationTypes.DuplicateActivity,
            ActorId = userId,
            ActorName = userName,
            ActivityId = activityId,
            ActivityName = activityName,
            CreatedAt = now,
        });

    /// Best-effort: a notification must never undo the action that triggered it.
    public static async Task TrySaveAsync(AppDb db, ILogger log, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.LogWarning(e, "Could not store a notification");
        }
    }
}
