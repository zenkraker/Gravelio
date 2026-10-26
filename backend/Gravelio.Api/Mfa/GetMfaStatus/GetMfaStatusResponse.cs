namespace Gravelio.Api.Mfa;

public sealed record GetMfaStatusResponse(bool Enabled, int RecoveryCodesLeft);
