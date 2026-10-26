namespace Gravelio.Api.Mfa;

/// Shown once: the only time the recovery codes can be read.
public sealed record EnableMfaResponse(string[] RecoveryCodes);
