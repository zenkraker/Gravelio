using System.Text.Json;
using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Activities.Import;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

/// <see cref="GearId"/> of null removes the gear from the activity.
public sealed record SetActivityGearRequest(int Id, int? GearId);
