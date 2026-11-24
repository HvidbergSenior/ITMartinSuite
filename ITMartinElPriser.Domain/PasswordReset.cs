using System.Security.Cryptography;
using System.Text;

namespace ITMartinElPriser.Domain;

/// <summary>
/// "Glemt adgangskoden?" (MinElpris, 2026-11-24). The link in the mail carries a random token; only its SHA-256 hash is
/// stored, so a copy of the database cannot be used to reset anyone's password. A ticket works once, for one hour, and a
/// new one can be asked for at most every two minutes per account (no mail-bombing).
/// </summary>
public sealed record PasswordResetTicket(string Token, string TokenHash, DateTime CreatedUtc, DateTime ExpiresUtc)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);
    public static readonly TimeSpan MinGap = TimeSpan.FromMinutes(2);

    public static PasswordResetTicket Create(DateTime nowUtc)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new PasswordResetTicket(token, Hash(token), nowUtc, nowUtc + Lifetime);
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? "")));

    /// <summary>A stored ticket is usable when it is unused and not expired.</summary>
    public static bool Usable(DateTime expiresUtc, DateTime? usedUtc, DateTime nowUtc) => usedUtc is null && nowUtc < expiresUtc;
}
