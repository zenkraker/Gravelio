using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

/// <see cref="Id"/> is the route id.
public sealed record UpdateGearRequest(int Id, string Nickname, string? Brand, string? Model, int GearType, bool Active = true,
    double InitialDistance = 0, decimal? PurchaseValue = null);
