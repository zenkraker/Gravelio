namespace Gravelio.Api.Goals;

public static class GoalRules
{
    public const int MaxTarget = 100_000_000;

    public static readonly string[] Intervals = ["daily", "weekly", "monthly", "yearly"];
    public static readonly string[] Metrics = ["calories", "activities", "distance", "elevation", "duration"];

    /// Which activity types each group counts. Walking also counts hiking and snowshoeing, and strength is the plain workout types.
    public static readonly IReadOnlyDictionary<string, int[]> Groups = new Dictionary<string, int[]>
    {
        ["run"] = [1, 2, 3, 34, 40],
        ["bike"] = [4, 5, 6, 7, 27, 28, 29, 35, 36],
        ["swim"] = [8, 9],
        ["walk"] = [11, 31, 12, 44],
        ["strength"] = [10, 19],
        ["cardio"] = [20, 41, 46, 47],
    };
}
