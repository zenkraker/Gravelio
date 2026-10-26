using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Gravelio.Api.Email;

/// Sends through the configured SMTP server. Unconfigured email is a logged no-op, not an error.
public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> log) : IEmailSender
{
    public bool IsConfigured => options.Value.IsConfigured;

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        var settings = options.Value;
        if (!IsConfigured)
        {
            log.LogWarning("Email is not configured; a '{Subject}' email to {Recipient} was not sent", subject, to);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress!, settings.FromName),
            Subject = subject,
            Body = body,
        };
        message.To.Add(to);
        using var client = new SmtpClient(settings.Host, settings.Port) { EnableSsl = settings.EnableSsl };
        if (!string.IsNullOrWhiteSpace(settings.Username))
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);
        await client.SendMailAsync(message, ct);
    }
}
