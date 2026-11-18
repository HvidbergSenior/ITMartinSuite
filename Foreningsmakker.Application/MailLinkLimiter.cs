using System.Collections.Concurrent;

namespace Foreningsmakker.Application;

/// <summary>
/// "Log ind med mail" may not be used to flood someone's inbox: at most 3 links per mail address per 15 minutes.
/// Counted BEFORE we look the mail up, so the answer never tells whether the mail is a member. In memory (restart clears it).
/// </summary>
public sealed class MailLinkLimiter(TimeProvider clock)
{
    public const int Max = 3;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _sent = new();

    /// <summary>True = allowed (and counted).</summary>
    public bool TryUse(string email)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var q = _sent.GetOrAdd(email, _ => new Queue<DateTime>());
        lock (q)
        {
            while (q.Count > 0 && now - q.Peek() > Window) q.Dequeue();
            if (q.Count >= Max) return false;
            q.Enqueue(now);
            return true;
        }
    }
}
