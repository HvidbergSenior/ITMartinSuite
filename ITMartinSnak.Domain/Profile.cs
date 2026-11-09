using System.Text.RegularExpressions;

namespace ITMartinSnak.Domain;

// 2026-11-09 "on each name the user should be able to add info. Mail etc" (Hold 36). Each participant fills in what they
// want to share - nothing is required, and only the people in the room see it. About = their own words on what they do
// (when empty, the line Martin pasted from the course's kursistoversigt is shown).
public sealed record Profile(string Name, string Mail = "", string Phone = "", string Web = "", string LinkedIn = "", string About = "");

public static partial class ProfileRules
{
    public const int MaxAbout = 300, MaxField = 120;

    [GeneratedRegex(@"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$")]
    private static partial Regex MailShape();

    /// <summary>The cleaned profile, or the reason it cannot be saved (a Danish sentence for the page).</summary>
    public static (Profile? Profile, string? Error) Clean(string? name, string? mail, string? phone, string? web, string? linkedIn, string? about)
    {
        var n = GroupRules.CleanName(name);
        if (n.Length == 0) return (null, "Vælg dit navn først.");
        var m = (mail ?? "").Trim();
        if (m.Length > 0 && (m.Length > MaxField || !MailShape().IsMatch(m))) return (null, "Mailadressen ser ikke rigtig ud.");
        var digits = new string((phone ?? "").Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (digits.Length > 0 && (digits.Count(char.IsDigit) < 8 || digits.Length > 15)) return (null, "Telefonnummeret skal have mindst 8 cifre.");
        var w = Url(web);
        if (w is null) return (null, "Hjemmesiden skal være en adresse som www.mitfirma.dk.");
        var li = Url(linkedIn);
        if (li is null) return (null, "LinkedIn skal være en adresse som linkedin.com/in/ditnavn.");
        var a = (about ?? "").Trim();
        if (a.Length > MaxAbout) a = a[..MaxAbout];
        return (new Profile(n, m, digits, w, li, a), null);
    }

    // "" stays "", "www.x.dk" becomes "https://www.x.dk/", anything that is not a web address = null (an error).
    private static string? Url(string? text)
    {
        var t = (text ?? "").Trim();
        if (t.Length == 0) return "";
        if (t.Length > MaxField) return null;
        if (!t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) t = "https://" + t;
        return Uri.TryCreate(t, UriKind.Absolute, out var u) && u.Host.Contains('.') && (u.Scheme == "https" || u.Scheme == "http") ? u.AbsoluteUri : null;
    }

    public static bool IsEmpty(Profile p) =>
        p.Mail.Length == 0 && p.Phone.Length == 0 && p.Web.Length == 0 && p.LinkedIn.Length == 0 && p.About.Length == 0;
}
