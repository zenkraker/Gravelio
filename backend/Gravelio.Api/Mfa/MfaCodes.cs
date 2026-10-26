using System.Text.RegularExpressions;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Mfa;

public enum MfaResult { Ok, Invalid, LockedOut }

/// Checks authenticator and recovery codes. Identity validates a code but would accept it again within its window,
/// so every accepted authenticator code is recorded and refused the second time (RFC 6238 §5.2).
public static partial class MfaCodes
{
    public const string LockedMessage = "Too many failed attempts. Try again later.";
    public const string InvalidMessage = "Incorrect password or code";

    // Identity accepts a code for +/- 2 thirty-second steps; keep the record well past that.
    static readonly TimeSpan Retention = TimeSpan.FromMinutes(10);

    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex SixDigits();

    /// An authenticator code, or one of the recovery codes shown when MFA was enabled. A wrong code counts toward the account lockout.
    public static async Task<MfaResult> VerifyAsync(UserManager<User> users, AppDb db, TimeProvider time, User user, string code, CancellationToken ct)
    {
        if (await users.IsLockedOutAsync(user)) return MfaResult.LockedOut;
        code = code.Replace(" ", string.Empty);
        var ok = SixDigits().IsMatch(code)
            ? await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code) && await FirstUseAsync(db, time, user.Id, code, ct)
            : (await users.RedeemTwoFactorRecoveryCodeAsync(user, code)).Succeeded;
        return await CountAsync(users, user, ok);
    }

    /// Confirms a sensitive change: the password, plus a code when the account has MFA on or <paramref name="requireCode"/> is set.
    public static async Task<MfaResult> StepUpAsync(UserManager<User> users, AppDb db, TimeProvider time, User user, string password, string? code,
        bool requireCode, CancellationToken ct)
    {
        if (await users.IsLockedOutAsync(user)) return MfaResult.LockedOut;
        if (!await users.CheckPasswordAsync(user, password)) return await CountAsync(users, user, ok: false);
        if (!user.TwoFactorEnabled && !requireCode) return await CountAsync(users, user, ok: true);
        return string.IsNullOrWhiteSpace(code) ? await CountAsync(users, user, ok: false) : await VerifyAsync(users, db, time, user, code, ct);
    }

    static async Task<MfaResult> CountAsync(UserManager<User> users, User user, bool ok)
    {
        if (ok)
        {
            await users.ResetAccessFailedCountAsync(user);
            return MfaResult.Ok;
        }
        await users.AccessFailedAsync(user);
        return MfaResult.Invalid;
    }

    static async Task<bool> FirstUseAsync(AppDb db, TimeProvider time, int userId, string code, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        await db.UsedTotpCodes.Where(c => c.UsedAt < now - Retention).ExecuteDeleteAsync(ct);
        var used = new UsedTotpCode { UserId = userId, Code = code, UsedAt = now };
        db.UsedTotpCodes.Add(used);
        if (await db.TrySaveUniqueAsync(ct)) return true;
        // Forget the rejected row, or the next save in this request (the lockout counter) would try to insert it again.
        db.Entry(used).State = EntityState.Detached;
        return false;
    }
}
