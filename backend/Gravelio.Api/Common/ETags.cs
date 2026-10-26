using Microsoft.AspNetCore.Http.Headers;
using Microsoft.Net.Http.Headers;

namespace Gravelio.Api.Common;

/// Optimistic concurrency over a row's PostgreSQL <c>xmin</c> (https://www.npgsql.org/efcore/modeling/concurrency.html): a response carries
/// it as the ETag, and an update that sends <c>If-Match</c> only goes through while the row is still that version.
public static class ETags
{
    public static string Format(uint version) => $"\"{version}\"";

    /// Adds the ETag to the response.
    public static void Set(HttpResponse response, uint version) => response.Headers.ETag = Format(version);

    /// True when the request names no version (the client did not ask for the check) or names this one.
    public static bool Satisfied(HttpRequest request, uint version)
    {
        var wanted = request.GetTypedHeaders().IfMatch;
        return wanted.Count == 0 || wanted.Any(t => t.Equals(EntityTagHeaderValue.Any) || t.Tag.Value == Format(version));
    }

    public const string Changed = "This was changed somewhere else. Reload it and try again.";
}
