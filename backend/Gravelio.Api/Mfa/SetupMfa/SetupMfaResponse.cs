namespace Gravelio.Api.Mfa;

/// The shared key to type into an authenticator app, and the same key as an otpauth:// link.
public sealed record SetupMfaResponse(string Secret, string Uri);
