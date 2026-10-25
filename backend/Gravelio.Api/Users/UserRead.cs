using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Users;

public sealed record UserRead(int Id, string Name, string Username, string Email, string AccessType, bool Active, string? City,
    DateOnly? Birthdate, string PreferredLanguage, string Gender, string Units, int? Height, int? MaxHeartRate,
    string FirstDayOfWeek, string Timezone, string Currency)
{
    public static UserRead From(User u, bool isAdmin) => new(u.Id, u.Name, u.UserName!, u.Email!, isAdmin ? AppRoles.Admin : "regular", u.Active,
        u.City, u.Birthdate, u.PreferredLanguage, u.Gender, u.Units, u.Height, u.MaxHeartRate, u.FirstDayOfWeek, u.Timezone, u.Currency);
}
