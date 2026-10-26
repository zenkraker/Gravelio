namespace Gravelio.Api.Auth;

/// Only sent when the password was right but the account has MFA on; a plain success has no body (204).
public sealed record LoginResponse(bool MfaRequired);
