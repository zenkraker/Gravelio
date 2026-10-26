namespace Gravelio.Api.Activities;

/// <see cref="HiddenDetails"/> null means "use my privacy settings"; a list (even an empty one) replaces them for this activity.
public sealed record UpdateActivityPrivateDetailsRequest(int Id, string? PrivateNotes, int? WorkoutFeeling, int? WorkoutRpe, string[]? HiddenDetails);
