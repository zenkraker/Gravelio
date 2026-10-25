using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed record DefaultGearSlot(int ActivityType, int GearType, int? GearId);
