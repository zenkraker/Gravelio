using FluentValidation;
using Gravelio.Api.Auth;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Users;

static class UserRules
{
    public static readonly string[] Genders = ["male", "female", "unspecified"];
    public static readonly string[] Units = ["metric", "imperial"];
    public static readonly string[] AccessTypes = ["regular", AppRoles.Admin];
    public static readonly string[] Currencies = ["euro", "dollar", "pound"];
    public static readonly string[] Days = ["sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"];

    // Letters, digits and dots, as in Endurain.
    public const string UsernamePattern = "^[a-zA-Z0-9.]{1,45}$";

    public static string Describe(IdentityResult result) => string.Join(" ", result.Errors.Select(e => e.Description));

    /// A taken username or email is a conflict; any other Identity failure (e.g. a weak password) is a bad request.
    public static int StatusOf(IdentityResult result) => result.Errors.Any(e => e.Code.StartsWith("Duplicate")) ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest;
}
