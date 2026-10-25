using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

/// <see cref="Id"/> is the gear id and <see cref="ComponentId"/> the component id; both come from the route.
public sealed record UpdateComponentRequest(int Id, int ComponentId, string Type, string? Brand, string? Model, DateOnly PurchaseDate,
    DateOnly? RetiredDate = null, bool Active = true, double? ExpectedDistance = null, decimal? PurchaseValue = null);
