using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace ITMartinSeMed.Domain;

// A connection to one browser. The domain only needs to know whether it is still open.
public interface ILine
{
    bool IsOpen { get; }
}

public enum JoinResult { Ok, Unknown, Taken, Expired, TooManyTries }

// One help session = Martin (helper) + one customer, found by a 6-digit code that Martin reads out on the phone.
// The screen and the voices go browser to browser and are never stored; this only knows who is on the line.
public sealed class HelpSession(string code, ILine helper, DateTime createdUtc)
{
    public string Code { get; } = code;
    // Null while Martin's line is down; his page reconnects with the same code within HelperGrace.
    public ILine? Helper { get; set; } = helper;
    public ILine? Customer { get; set; }
    public DateTime? HelperGone { get; set; }
    // Moved forward when the customer drops out, so a reload mid-session can rejoin with the same code.
    public DateTime Created { get; set; } = createdUtc;
}

public static class SessionRules
{
    // A code nobody joined is dead after this long, so an old code read out last week opens nothing.
    public static readonly TimeSpan JoinWindow = TimeSpan.FromMinutes(30);
    // Wrong codes per address before it has to wait: 1,000,000 codes, so guessing gets nowhere.
    public const int MaxBadTries = 10;
    public static readonly TimeSpan BadTryWindow = TimeSpan.FromMinutes(10);
    // How long a session waits for Martin's page to come back after its connection dropped.
    public static readonly TimeSpan HelperGrace = TimeSpan.FromMinutes(10);

    /// <summary>"123 456" / "123-456" -> "123456".</summary>
    public static string NormalizeCode(string? code) => new((code ?? "").Where(char.IsDigit).ToArray());

    public static string NewCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}

// The open sessions (in memory - a session lives for one phone call) and the rules for joining, dropping out and coming back.
public sealed class SessionRegistry(TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, HelpSession> _sessions = new();
    private readonly ConcurrentDictionary<string, List<DateTime>> _bad = new();

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public int Count => _sessions.Count;

    public HelpSession Create(ILine helper)
    {
        while (true)
        {
            var s = new HelpSession(SessionRules.NewCode(), helper, Now);
            if (_sessions.TryAdd(s.Code, s)) return s;
        }
    }

    public (JoinResult Result, HelpSession? Session) Join(string? code, string ip, ILine customer)
    {
        var now = Now;
        var tries = _bad.GetOrAdd(ip, _ => []);
        lock (tries)
        {
            tries.RemoveAll(t => now - t > SessionRules.BadTryWindow);
            if (tries.Count >= SessionRules.MaxBadTries) return (JoinResult.TooManyTries, null);
        }
        var c = SessionRules.NormalizeCode(code);
        if (!_sessions.TryGetValue(c, out var s))
        {
            lock (tries) tries.Add(now);
            return (JoinResult.Unknown, null);
        }
        lock (s)
        {
            // A line that is no longer open is her own dropped line, so her reconnect takes its place.
            if (s.Customer is { IsOpen: true }) return (JoinResult.Taken, null);
            if (now - s.Created > SessionRules.JoinWindow) { _sessions.TryRemove(c, out _); return (JoinResult.Expired, null); }
            s.Customer = customer;
        }
        return (JoinResult.Ok, s);
    }

    public void End(HelpSession s) => _sessions.TryRemove(s.Code, out _);

    // Martin's page lost its connection: keep the session, so the customer's screen and code stay valid.
    public void LeaveHelper(HelpSession s, ILine helper)
    {
        lock (s)
        {
            if (!ReferenceEquals(s.Helper, helper)) return;   // he already came back on a new line
            s.Helper = null;
            s.HelperGone = Now;
        }
    }

    // Martin's page reconnecting with its own code. Null when the session is gone or too old.
    public HelpSession? Resume(string? code, ILine helper)
    {
        var c = SessionRules.NormalizeCode(code);
        if (!_sessions.TryGetValue(c, out var s)) return null;
        lock (s)
        {
            if (s.HelperGone is { } gone && Now - gone > SessionRules.HelperGrace) { _sessions.TryRemove(c, out _); return null; }
            s.Helper = helper;
            s.HelperGone = null;
        }
        return s;
    }

    // True when Martin did not come back within HelperGrace; the session is then removed.
    public bool HelperTimedOut(HelpSession s)
    {
        lock (s)
        {
            if (s.HelperGone is not { } gone || Now - gone < SessionRules.HelperGrace) return false;
        }
        End(s);
        return true;
    }

    public void LeaveCustomer(HelpSession s)
    {
        lock (s) { s.Customer = null; s.Created = Now; }
    }
}
