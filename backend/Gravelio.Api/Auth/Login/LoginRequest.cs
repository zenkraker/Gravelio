using FluentValidation;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Auth;

public sealed record LoginRequest(string Username, string Password);
