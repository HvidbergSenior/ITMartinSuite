namespace ITMartinSnak.Domain;

// 2026-11-09 "a way to show that you have seen the message ... so you know people read your chat" + "as admin a view on
// who has slå beskeder til/fra and other relevant stuff to help setup". One row per name in a room: how far they have
// read, when they were last in, and how their phone is set up. Only setup facts - nothing about what they do elsewhere.
public sealed record Seen(string Name, long MessageId, DateTime At, string Device = "", bool HomeScreen = false,
    string Push = "", bool InApp = false);

public static class SeenRules
{
    // What the page may say about its phone. Anything else is stored as "".
    public static readonly string[] Devices = ["iphone", "android", "computer"];
    public static readonly string[] PushStates = ["til", "fra", "nej", "ikke-spurgt", "kan-ikke"];

    // A page open on a phone pings now and then; only write to disk when something changed or it is a while ago.
    public static readonly TimeSpan RewriteAfter = TimeSpan.FromMinutes(10);

    public static Seen? Clean(string? name, long messageId, DateTime now, string? device, bool homeScreen, string? push, bool inApp)
    {
        var n = GroupRules.CleanName(name);
        if (n.Length == 0) return null;
        var d = (device ?? "").Trim().ToLowerInvariant();
        var p = (push ?? "").Trim().ToLowerInvariant();
        return new Seen(n, Math.Max(0, messageId), now, Devices.Contains(d) ? d : "", homeScreen, PushStates.Contains(p) ? p : "", inApp);
    }

    // Read position only moves forward (an old phone with a stale page never "un-reads").
    public static Seen Merge(Seen? old, Seen fresh) =>
        old is null ? fresh : fresh with { MessageId = Math.Max(old.MessageId, fresh.MessageId) };

    public static bool WorthSaving(Seen? old, Seen merged) =>
        old is null || old.MessageId != merged.MessageId || old with { At = merged.At } != merged || merged.At - old.At >= RewriteAfter;
}
