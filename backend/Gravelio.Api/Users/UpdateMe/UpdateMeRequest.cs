using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Users;

public sealed record UpdateMeRequest(string Name, string Email, string? City, DateOnly? Birthdate, string PreferredLanguage,
    string Gender, string Units, int? Height, int? MaxHeartRate, string FirstDayOfWeek, string Timezone, string Currency = "euro");
