using System.ComponentModel.DataAnnotations;

namespace Gravelio.Api.Email;

/// SMTP settings. Email is optional: without a host and sender address the app logs and skips sending, so every feature
/// that emails must also work without it (password reset just tells nobody).
public sealed class EmailOptions
{
    public string? Host { get; set; }

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? FromAddress { get; set; }
    public string FromName { get; set; } = "Gravelio";
    public bool EnableSsl { get; set; } = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
}
