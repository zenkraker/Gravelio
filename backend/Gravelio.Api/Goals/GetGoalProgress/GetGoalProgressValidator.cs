using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Goals;

public sealed class GetGoalProgressValidator : Validator<GetGoalProgressRequest>
{
    public GetGoalProgressValidator() =>
        RuleFor(x => x.Date).Must(d => d is null || (d >= new DateOnly(1900, 1, 1) && d < new DateOnly(2200, 1, 1))).WithMessage("Date is out of range");
}
