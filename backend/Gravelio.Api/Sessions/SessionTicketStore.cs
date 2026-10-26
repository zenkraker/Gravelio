using System.Security.Claims;
using System.Security.Cryptography;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Sessions;

/// Keeps the auth ticket in <c>UserSessions</c> instead of the cookie (https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.authentication.cookies.iticketstore).
/// A session is ended by deleting its row; expired rows are swept whenever a new session starts.
/// shortcut: the security-stamp validator (interval zero, so revocation is immediate) renews the ticket on every request, which
/// is one UPDATE per request and is also what keeps LastSeenAt current; raise the interval and accept slower revocation if that ever hurts.
public sealed class SessionTicketStore(IServiceScopeFactory scopes, IHttpContextAccessor http, TimeProvider time) : ITicketStore
{
    /// Claim added to a retrieved ticket so an endpoint can tell which session is the caller's own.
    public const string SessionIdClaim = "sid";

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var now = time.GetUtcNow().UtcDateTime;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        await db.UserSessions.Where(s => s.ExpiresAt < now).ExecuteDeleteAsync();

        var request = http.HttpContext;
        var key = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        db.UserSessions.Add(new UserSession
        {
            UserId = int.Parse(ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!),
            Key = key,
            Ticket = TicketSerializer.Default.Serialize(Clean(ticket)),
            Device = UserAgentInfo.Describe(request?.Request.Headers.UserAgent.ToString()),
            IpAddress = request?.Connection.RemoteIpAddress?.ToString(),
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = ticket.Properties.ExpiresUtc?.UtcDateTime ?? now.AddDays(1),
        });
        await db.SaveChangesAsync();
        return key;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var now = time.GetUtcNow().UtcDateTime;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var bytes = TicketSerializer.Default.Serialize(Clean(ticket));
        var expires = ticket.Properties.ExpiresUtc?.UtcDateTime ?? now.AddDays(1);
        await db.UserSessions.Where(s => s.Key == key)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Ticket, bytes).SetProperty(x => x.ExpiresAt, expires).SetProperty(x => x.LastSeenAt, now));
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        var now = time.GetUtcNow().UtcDateTime;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var row = await db.UserSessions.AsNoTracking().Where(s => s.Key == key && s.ExpiresAt > now)
            .Select(s => new { s.Id, s.Ticket }).FirstOrDefaultAsync();
        if (row is null) return null;

        var ticket = TicketSerializer.Default.Deserialize(row.Ticket);
        ticket?.Principal.Identities.First().AddClaim(new Claim(SessionIdClaim, row.Id.ToString()));
        return ticket;
    }

    public async Task RemoveAsync(string key)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDb>().UserSessions.Where(s => s.Key == key).ExecuteDeleteAsync();
    }

    // The session id is derived on every read, never stored with the ticket.
    static AuthenticationTicket Clean(AuthenticationTicket ticket)
    {
        foreach (var identity in ticket.Principal.Identities)
            foreach (var claim in identity.FindAll(SessionIdClaim).ToList()) identity.RemoveClaim(claim);
        return ticket;
    }
}
