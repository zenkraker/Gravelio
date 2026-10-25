using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

/// <see cref="Id"/> is the gear id from the route.
public sealed record CreateComponentRequest(int Id, string Type, string? Brand, string? Model, DateOnly PurchaseDate,
    DateOnly? RetiredDate = null, bool Active = true, double? ExpectedDistance = null, decimal? PurchaseValue = null);
