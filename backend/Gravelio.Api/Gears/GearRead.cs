using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed record GearRead(int Id, string Nickname, string? Brand, string? Model, int GearType, bool Active, double InitialDistance,
    decimal? PurchaseValue, DateTime CreatedAt, double TotalDistance, int ActivityCount);
