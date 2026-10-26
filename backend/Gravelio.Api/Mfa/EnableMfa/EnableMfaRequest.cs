namespace Gravelio.Api.Mfa;

public sealed record EnableMfaRequest(string CurrentPassword, string Code);
