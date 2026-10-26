using System.Linq.Expressions;
using Gravelio.Api.Data;

namespace Gravelio.Api.Notifications;

public sealed record NotificationRead(int Id, int Type, int ActorId, string ActorName, bool Read, DateTime CreatedAt, int? ActivityId, string? ActivityName)
{
    public static readonly Expression<Func<Notification, NotificationRead>> Projection =
        n => new NotificationRead(n.Id, n.Type, n.ActorId, n.ActorName, n.Read, n.CreatedAt, n.ActivityId, n.ActivityName);
}
