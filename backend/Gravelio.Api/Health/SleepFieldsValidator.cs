using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Health;

/// The ranges Endurain enforces, shared by the create and update validators.
public abstract class SleepFieldsValidator<T> : Validator<T> where T : ISleepFields
{
    protected SleepFieldsValidator()
    {
        RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("Enter the date");
        RuleFor(x => x.TotalSleepSeconds).GreaterThanOrEqualTo(0);
        RuleFor(x => x.NapTimeSeconds).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UnmeasurableSleepSeconds).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DeepSleepSeconds).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LightSleepSeconds).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RemSleepSeconds).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AwakeSleepSeconds).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AvgHeartRate).InclusiveBetween(20, 220);
        RuleFor(x => x.MinHeartRate).InclusiveBetween(20, 220);
        RuleFor(x => x.MaxHeartRate).InclusiveBetween(20, 220);
        RuleFor(x => x.RestingHeartRate).InclusiveBetween(20, 220);
        RuleFor(x => x.AvgSpo2).InclusiveBetween(70, 100);
        RuleFor(x => x.LowestSpo2).InclusiveBetween(70, 100);
        RuleFor(x => x.HighestSpo2).InclusiveBetween(70, 100);
        RuleFor(x => x.AvgRespiration).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LowestRespiration).GreaterThanOrEqualTo(0);
        RuleFor(x => x.HighestRespiration).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AvgStressLevel).InclusiveBetween(0, 100);
        RuleFor(x => x.AwakeCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RestlessMomentsCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AvgSleepStress).InclusiveBetween(0, 100);
        RuleFor(x => x)
            .Must(x => x.SleepStartTimeGmt is null || x.SleepEndTimeGmt is null || x.SleepStartTimeGmt < x.SleepEndTimeGmt)
            .WithMessage("Sleep start time must be before sleep end time");
        RuleFor(x => x)
            .Must(x => x.SleepStartTimeLocal is null || x.SleepEndTimeLocal is null || x.SleepStartTimeLocal < x.SleepEndTimeLocal)
            .WithMessage("Sleep start time (local) must be before sleep end time (local)");
    }
}
