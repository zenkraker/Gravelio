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
