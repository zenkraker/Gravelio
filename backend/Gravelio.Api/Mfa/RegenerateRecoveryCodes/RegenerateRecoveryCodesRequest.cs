namespace Gravelio.Api.Mfa;

public sealed record RegenerateRecoveryCodesRequest(string CurrentPassword, string Code);
