namespace Gravelio.Api.Activities;

/// <see cref="IsHidden"/> hides the whole activity from everyone but the owner (and from statistics and goals).
/// <see cref="HiddenDetails"/> null means "use my privacy settings"; a list (even an empty one) replaces them for this activity.
public sealed record UpdateActivityPrivateDetailsRequest(int Id, string? PrivateNotes, int? WorkoutFeeling, int? WorkoutRpe, string[]? HiddenDetails, bool IsHidden = false);
