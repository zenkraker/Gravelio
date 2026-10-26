using System.Text.Json;

namespace Gravelio.Api.Activities;

public sealed record StreamRead(int StreamType, JsonElement Waypoints);
