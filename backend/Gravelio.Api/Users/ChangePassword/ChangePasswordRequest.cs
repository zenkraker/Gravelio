using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Users;

/// <see cref="Code"/> (authenticator or recovery code) is required while the account has MFA on.
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string? Code = null);
