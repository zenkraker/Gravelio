using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed record CreateGearRequest(string Nickname, string? Brand, string? Model, int GearType, bool Active = true,
    double InitialDistance = 0, decimal? PurchaseValue = null);
