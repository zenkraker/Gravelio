using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Users;

public sealed record UpdateUserRequest(int Id, string Name, string Username, string Email, string AccessType, bool Active, string? Password);
