using System.Security.Cryptography;
using System.Text;

namespace ITMartinSnak.Domain;

// The shapes are kept exactly as they were stored in messages.json / tasks.json since 2026-10-09 - existing files read as before.
public sealed record Message(long Id, string Name, string Text, DateTime At);

public sealed record TaskItem(long Id, string Title, string Note, string When, List<string> Takers, bool Done, DateTime CreatedAt);

// The group's rules (user 2026-10-09: a small chat for a few friends instead of a Facebook group; Martin makes the tasks,
// the others chat, see the tasks and put themselves on one).
public static class GroupRules
{
    public const int MaxMessages = 5000, MaxText = 4000, MaxName = 30, MaxTitle = 120, MaxNote = 1000, MaxWhen = 60;

    public static string CleanName(string? name) => Cut((name ?? "").Trim(), MaxName);

    private static string Cut(string s, int max) => s.Length > max ? s[..max] : s;

    /// <returns>The new message, or null when the name or the text is missing.</returns>
    public static Message? NewMessage(IReadOnlyList<Message> existing, string? name, string? text, DateTime nowUtc)
    {
        var n = CleanName(name);
        var t = Cut((text ?? "").Trim(), MaxText);
        if (n.Length == 0 || t.Length == 0) return null;
        return new Message((existing.Count == 0 ? 0 : existing[^1].Id) + 1, n, t, nowUtc);
    }

    /// <summary>Only the newest <see cref="MaxMessages"/> are kept.</summary>
    public static void Trim(List<Message> messages)
    {
        if (messages.Count > MaxMessages) messages.RemoveRange(0, messages.Count - MaxMessages);
    }

    public static TaskItem? NewTask(IReadOnlyList<TaskItem> existing, string? title, string? note, string? when, DateTime nowUtc)
    {
        var ti = (title ?? "").Trim();
        if (ti.Length == 0) return null;
        return new TaskItem((existing.Count == 0 ? 0 : existing.Max(x => x.Id)) + 1, Cut(ti, MaxTitle), Cut((note ?? "").Trim(), MaxNote),
            Cut((when ?? "").Trim(), MaxWhen), [], false, nowUtc);
    }

    public static TaskItem? Edit(TaskItem t, string? title, string? note, string? when)
    {
        var ti = (title ?? "").Trim();
        if (ti.Length == 0) return null;
        return t with { Title = Cut(ti, MaxTitle), Note = Cut((note ?? "").Trim(), MaxNote), When = Cut((when ?? "").Trim(), MaxWhen) };
    }

    /// <summary>Puts the name on the task, or takes it off again if it is already there (case doesn't matter).</summary>
    public static TaskItem? ToggleTaker(TaskItem t, string? name)
    {
        var n = CleanName(name);
        if (n.Length == 0) return null;
        var takers = t.Takers.ToList();
        var i = takers.FindIndex(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase));
        if (i >= 0) takers.RemoveAt(i); else takers.Add(n);
        return t with { Takers = takers };
    }

    public static bool HasTaker(TaskItem t, string? name) =>
        t.Takers.Contains(CleanName(name), StringComparer.OrdinalIgnoreCase);

    public static TaskItem Copy(TaskItem t) => t with { Takers = t.Takers.ToList() };
}

// Who may do what: the group code makes a member, the admin code (Martin) also makes, edits and removes tasks.
public static class AccessRules
{
    public const int MinGroupCode = 4, MinAdminCode = 6, MaxWrongTries = 10;
    public static readonly TimeSpan WrongTriesWindow = TimeSpan.FromMinutes(15);

    /// <summary>The cookie value: a hash, so the code itself never sits in the browser.</summary>
    public static string Token(string secret, string kind) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(kind + ":" + secret)));
}
