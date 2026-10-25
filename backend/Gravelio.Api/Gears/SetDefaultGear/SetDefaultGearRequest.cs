using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

/// <see cref="GearId"/> of null clears the default for that activity type.
public sealed record SetDefaultGearRequest(int ActivityType, int? GearId);
