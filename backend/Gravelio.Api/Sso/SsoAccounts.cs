using System.Text.RegularExpressions;
using Gravelio.Api.Data;
using Gravelio.Api.Users;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Sso;

public static partial class SsoAccounts
{
    /// A login name the provider's claims suggest, made to fit <see cref="UserRules.UsernamePattern"/> and not taken yet.
    public static async Task<string> UniqueUsernameAsync(UserManager<User> users, string? preferred, string email)
    {
        var seed = string.IsNullOrWhiteSpace(preferred) ? email.Split('@')[0] : preferred;
        var name = Trim(NotAllowed().Replace(seed, "."));
        if (name.Length == 0) name = "user";
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = attempt == 0 ? name : $"{name[..Math.Min(name.Length, 38)]}{Random.Shared.Next(100, 99999)}";
            if (await users.FindByNameAsync(candidate) is null) return candidate;
        }
        return $"user{Guid.NewGuid():N}"[..20];

        static string Trim(string s) => s.Trim('.')[..Math.Min(s.Trim('.').Length, 40)];
    }

    [GeneratedRegex("[^a-zA-Z0-9.]+")]
    private static partial Regex NotAllowed();
}
