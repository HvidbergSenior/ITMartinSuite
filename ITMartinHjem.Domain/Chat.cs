using System.Text.RegularExpressions;

namespace ITMartinHjem.Domain;

public sealed class ChatThread
{
    public int Id { get; set; }
    public string VisitorKey { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime LastAt { get; set; } = DateTime.UtcNow;
    public bool UnreadForOwner { get; set; }
    // How the visitor hears that Martin answered (both optional, given in the chat):
    public string VisitorEmail { get; set; } = "";
    public string VisitorPush { get; set; } = "";          // "endpoint|p256dh|auth"
    public DateTime? NotifiedAt { get; set; }               // last reply the visitor was told about
    // When Martin last got the "venter på svar" copy by mail (2026-10-08: kept in the DB, so a restart never mails twice).
    public DateTime? OwnerMailedAt { get; set; }
    public List<ChatMessage> Messages { get; set; } = [];

    public bool IsPilot => VisitorKey.StartsWith(ChatRules.PilotPrefix);
    public string Who => Name.Length > 0 ? Name : "En besøgende";

    public DateTime? LastFromOwner => Messages.Where(m => m.FromOwner).Select(m => (DateTime?)m.At).Max();
    public DateTime? LastFromVisitor => Messages.Where(m => !m.FromOwner).Select(m => (DateTime?)m.At).Max();

    /// <summary>Martin's replies the visitor has not been told about yet.</summary>
    public List<string> UntoldReplies()
    {
        var since = NotifiedAt ?? DateTime.MinValue;
        return Messages.Where(m => m.FromOwner && m.At > since).OrderBy(m => m.At).Select(m => m.Text).ToList();
    }

    /// <summary>What the visitor wrote after Martin's last reply.</summary>
    public List<string> UnansweredFromVisitor()
    {
        var lastOwner = LastFromOwner ?? DateTime.MinValue;
        return Messages.Where(m => !m.FromOwner && m.At > lastOwner).OrderBy(m => m.At).Select(m => m.Text).ToList();
    }

    // Tell the visitor 90 s after Martin's LAST reply (several quick replies become one mail) - only when they can be reached.
    public bool VisitorIsDue(DateTime nowUtc) =>
        LastFromOwner is { } o && o > nowUtc - ChatRules.Recent && o > (NotifiedAt ?? DateTime.MinValue) && nowUtc - o >= ChatRules.VisitorDelay
        && (VisitorEmail.Length > 0 || VisitorPush.Length > 0);

    // Mail Martin a copy when a visitor's message is still unread in Svar after 2 minutes.
    public bool OwnerCopyIsDue(DateTime nowUtc) =>
        UnreadForOwner && LastFromVisitor is { } v && v > nowUtc - ChatRules.Recent && v > (OwnerMailedAt ?? DateTime.MinValue) && nowUtc - v >= ChatRules.OwnerDelay;
}

public sealed class ChatMessage
{
    public int Id { get; set; }
    public int ThreadId { get; set; }
    public ChatThread Thread { get; set; } = null!;
    public bool FromOwner { get; set; }
    public string Text { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public static partial class ChatRules
{
    public const int MaxText = 2000, MaxName = 60;
    public const string PilotPrefix = "pilot-", TrialPrefix = "proev-";

    // The sweep finds due mails in the DATABASE every 15 s (2026-10-08): in-memory timers lost the mail on a deploy within 90 s.
    public static readonly TimeSpan VisitorDelay = TimeSpan.FromSeconds(90);
    public static readonly TimeSpan OwnerDelay = TimeSpan.FromMinutes(2);
    // Only recent messages count, so old conversations never wake up and mail after a deploy.
    public static readonly TimeSpan Recent = TimeSpan.FromHours(24);

    // A visitor key is made by the browser (or by the pilot/trial forms) - anything else is refused.
    public static bool ValidKey(string key) => key.Length is >= 8 and <= 64;

    public static string Trim(string? s, int max = MaxText) { s = (s ?? "").Trim(); return s.Length > max ? s[..max] : s; }

    public static string Preview(string text) => text.Length > 120 ? text[..120] + "…" : text;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")] private static partial Regex Email();

    public static bool LooksLikeEmail(string s) => s.Length is > 5 and < 200 && Email().IsMatch(s);
}
