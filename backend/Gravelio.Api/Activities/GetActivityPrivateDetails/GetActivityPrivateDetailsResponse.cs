namespace Gravelio.Api.Activities;

public sealed record GetActivityPrivateDetailsResponse(string? PrivateNotes, int? WorkoutFeeling, int? WorkoutRpe, string[]? HiddenDetails);
