namespace Gravelio.Api.Auth;

/// <see cref="Code"/> is an authenticator code or a recovery code.
public sealed record VerifyMfaRequest(string Code);
