using System.Security.Cryptography;
using System.Text;

namespace ITMartinForloebet.Server.Services;

public static class Slugs
{
    private const string Alphabet = "abcdefghjkmnpqrstuvwxyz23456789";

    /// <summary>"kontrolafgift-movia-7k3m" - readable, but the suffix makes it unguessable enough
    /// that the public page is not enumerable.</summary>
    public static string ForForloeb(string omraade, string modpart)
    {
        var stem = Clean(omraade) + "-" + Clean(modpart);
        return stem.Trim('-') + "-" + Random(4);
    }

    public static string EditKey() => Random(22);

    public static string Random(int len)
    {
        var bytes = RandomNumberGenerator.GetBytes(len);
        var sb = new StringBuilder(len);
        foreach (var b in bytes) sb.Append(Alphabet[b % Alphabet.Length]);
        return sb.ToString();
    }

    private static string Clean(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s.ToLowerInvariant())
        {
            var c = ch switch { 'æ' => "ae", 'ø' => "oe", 'å' => "aa", _ => char.IsLetterOrDigit(ch) ? ch.ToString() : "-" };
            sb.Append(c);
        }
        var r = sb.ToString();
        while (r.Contains("--")) r = r.Replace("--", "-");
        return r.Length > 24 ? r[..24].Trim('-') : r.Trim('-');
    }
}
