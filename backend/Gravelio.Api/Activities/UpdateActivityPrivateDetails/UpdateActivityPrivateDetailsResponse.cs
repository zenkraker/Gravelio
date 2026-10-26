namespace Gravelio.Api.Activities;

public sealed record UpdateActivityPrivateDetailsResponse(string? PrivateNotes, int? WorkoutFeeling, int? WorkoutRpe, string[]? HiddenDetails, bool IsHidden = false);
