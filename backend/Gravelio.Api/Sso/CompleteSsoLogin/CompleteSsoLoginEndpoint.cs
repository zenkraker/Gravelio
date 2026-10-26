using System.Security.Claims;
using FastEndpoints;
using Gravelio.Api.Common;
using Gravelio.Api.Data;
using Gravelio.Api.Settings;
using Gravelio.Api.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Sso;

/// Where the identity provider's answer ends up. The handler has already checked the code, state, nonce and the id token's signature;
/// this decides which Gravelio account that identity is. An identity is only ever matched by the (provider, subject) pair a person linked
/// on purpose, never by email alone: an address the provider vouches for may still belong to someone else's account here.
public sealed class CompleteSsoLoginEndpoint(AppDb db, UserManager<User> users, SignInManager<User> signIn, SsoProviders sso, TimeProvider time,
    ILogger<CompleteSsoLoginEndpoint> log) : Endpoint<CompleteSsoLoginRequest>
{
    public override void Configure()
    {
        Get("/auth/sso/{slug}/complete");
        AllowAnonymous();
        Options(o => o.RequireRateLimiting(RateLimiting.Sensitive));
    }

    public override async Task HandleAsync(CompleteSsoLoginRequest req, CancellationToken ct)
    {
        var provider = sso.Find(req.Slug);
        var external = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme); // the external cookie is good for this one visit only
        var subject = external.Principal?.FindFirstValue("sub");
        if (provider is null || !external.Succeeded || external.Principal is null || external.Properties is null || string.IsNullOrEmpty(subject)
            || !external.Properties.Items.TryGetValue("sso_provider", out var from) || from != req.Slug)
        {
            await Send.RedirectAsync("/login?sso_error=failed", allowRemoteRedirects: false);
            return;
        }

        // Linking an account the person is already signed in to (the id travels inside the protected state, so it cannot be swapped).
        if (external.Properties.Items.TryGetValue("sso_link_user", out var linkUser) && linkUser is not null)
        {
            await Send.RedirectAsync(await LinkAsync(linkUser, provider, subject), allowRemoteRedirects: false);
            return;
        }

        var user = await users.FindByLoginAsync(provider.Slug, subject);
        if (user is null)
        {
            var (created, error) = await TryCreateAsync(provider, external.Principal, subject, ct);
            if (error is not null)
            {
                await Send.RedirectAsync($"/login?sso_error={error}", allowRemoteRedirects: false);
                return;
            }
            user = created!;
        }
        if (!user.Active)
        {
            await Send.RedirectAsync($"/login?sso_error={(user.PendingApproval ? "pending_approval" : "inactive")}", allowRemoteRedirects: false);
            return;
        }
        if (await users.IsLockedOutAsync(user))
        {
            await Send.RedirectAsync("/login?sso_error=locked", allowRemoteRedirects: false);
            return;
        }
        // The identity provider has authenticated this person; its own second factor, if any, is its business. A local MFA code is not asked again.
        await signIn.SignInAsync(user, isPersistent: true);
        await Send.RedirectAsync("/", allowRemoteRedirects: false);
    }

    async Task<string> LinkAsync(string linkUserId, SsoProviderConfig provider, string subject)
    {
        var user = await users.FindByIdAsync(linkUserId);
        if (user is not { Active: true }) return "/security?sso_error=failed";
        var owner = await users.FindByLoginAsync(provider.Slug, subject);
        if (owner is not null) return owner.Id == user.Id ? "/security?sso=linked" : "/security?sso_error=taken";
        var result = await users.AddLoginAsync(user, new UserLoginInfo(provider.Slug, subject, provider.Name));
        return result.Succeeded ? "/security?sso=linked" : "/security?sso_error=failed";
    }

    /// A first sign-in with a provider that creates accounts: only for an email the provider says it has verified and that no account uses.
    async Task<(User? User, string? Error)> TryCreateAsync(SsoProviderConfig provider, ClaimsPrincipal principal, string subject, CancellationToken ct)
    {
        if (!provider.AutoCreateUsers) return (null, "not_linked");
        var email = principal.FindFirstValue("email")?.Trim();
        var verified = string.Equals(principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(email) || !verified) return (null, "email_unverified");
        if (await users.FindByEmailAsync(email) is not null) return (null, "email_in_use");

        var settings = await ServerSettingsStore.GetAsync(db, ct);
        var username = await SsoAccounts.UniqueUsernameAsync(users, principal.FindFirstValue("preferred_username"), email);
        var name = principal.FindFirstValue("name")?.Trim();
        var user = new User
        {
            Name = string.IsNullOrEmpty(name) ? username : name[..Math.Min(name.Length, 250)],
            UserName = username,
            Email = email,
            EmailConfirmed = true, // the provider vouched for it
            Active = !settings.SignupRequireAdminApproval,
            PendingApproval = settings.SignupRequireAdminApproval,
            CreatedAt = time.GetUtcNow().UtcDateTime,
        };
        // No password: this account signs in through the provider (and can set a password later with "forgot password").
        var result = await users.CreateAsync(user);
        if (result.Succeeded) result = await users.AddLoginAsync(user, new UserLoginInfo(provider.Slug, subject, provider.Name));
        if (!result.Succeeded)
        {
            log.LogWarning("Could not create the account for a new {Provider} sign-in: {Errors}", provider.Slug, string.Join("; ", result.Errors.Select(e => e.Code)));
            return (null, "failed");
        }
        if (user.PendingApproval) await SignupApprovals.NotifyAdminsAsync(db, users, user, time.GetUtcNow().UtcDateTime, log, ct);
        return (user, null);
    }
}
