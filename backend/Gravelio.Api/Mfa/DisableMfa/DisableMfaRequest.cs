namespace Gravelio.Api.Mfa;

/// <see cref="Code"/> is an authenticator code or a recovery code.
public sealed record DisableMfaRequest(string CurrentPassword, string Code);
