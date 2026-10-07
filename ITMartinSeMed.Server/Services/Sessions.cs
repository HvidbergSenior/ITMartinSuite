using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace ITMartinSeMed.Server.Services;

public enum JoinResult { Ok, Unknown, Taken, Expired, TooManyTries }

// One help session = Martin (helper) + one customer, found by a 6-digit code that Martin reads
// out on the phone. The server only passes the WebRTC handshake (offer/answer/ICE) between the
// two sockets; the screen and the voices go browser to browser and are never stored here.
public sealed class Session(string code, WebSocket helper, DateTime created)
{
    public string Code { get; } = code;
    // Null while Martin's line is down; his page reconnects with the same code within HelperGrace.
    public WebSocket? Helper { get; set; } = helper;
    public WebSocket? Customer { get; set; }
    public DateTime? HelperGone { get; set; }
    // Moved forward when the customer drops out, so a reload mid-session can rejoin with the same code.
    public DateTime Created { get; set; } = created;
    public readonly SemaphoreSlim SendLock = new(1, 1);
}

public sealed class Sessions(TimeProvider clock)
{
    // A code nobody joined is dead after this long, so an old code read out last week opens nothing.
    public static readonly TimeSpan JoinWindow = TimeSpan.FromMinutes(30);
    // Wrong codes per address before it has to wait: 1,000,000 codes, so guessing gets nowhere.
    public const int MaxBadTries = 10;
    // How long a session waits for Martin's page to come back after its connection dropped.
    public static readonly TimeSpan HelperGrace = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan BadTryWindow = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, Session> _sessions = new();
    private readonly ConcurrentDictionary<string, List<DateTime>> _bad = new();

    public Session Create(WebSocket helper)
    {
        while (true)
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var s = new Session(code, helper, clock.GetUtcNow().UtcDateTime);
            if (_sessions.TryAdd(code, s)) return s;
        }
    }

    public (JoinResult result, Session? session) Join(string? code, string ip, WebSocket customer)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var tries = _bad.GetOrAdd(ip, _ => []);
        lock (tries)
        {
            tries.RemoveAll(t => now - t > BadTryWindow);
            if (tries.Count >= MaxBadTries) return (JoinResult.TooManyTries, null);
        }

        code = new string((code ?? "").Where(char.IsDigit).ToArray());
        if (!_sessions.TryGetValue(code, out var s))
        {
            lock (tries) tries.Add(now);
            return (JoinResult.Unknown, null);
        }
        lock (s)
        {
            // A socket that is no longer open is her own dropped line, so her reconnect takes its place.
            if (s.Customer is { State: WebSocketState.Open }) return (JoinResult.Taken, null);
            if (now - s.Created > JoinWindow) { _sessions.TryRemove(code, out _); return (JoinResult.Expired, null); }
            s.Customer = customer;
        }
        return (JoinResult.Ok, s);
    }

    public void End(Session s) => _sessions.TryRemove(s.Code, out _);

    // Martin's page lost its connection: keep the session, so the customer's screen and code stay valid.
    public void LeaveHelper(Session s, WebSocket helper)
    {
        lock (s)
        {
            if (!ReferenceEquals(s.Helper, helper)) return;   // he already came back on a new line
            s.Helper = null;
            s.HelperGone = clock.GetUtcNow().UtcDateTime;
        }
    }

    // Martin's page reconnecting with its own code. Null when the session is gone or too old.
    public Session? Resume(string? code, WebSocket helper)
    {
        code = new string((code ?? "").Where(char.IsDigit).ToArray());
        if (!_sessions.TryGetValue(code, out var s)) return null;
        lock (s)
        {
            if (s.HelperGone is { } gone && clock.GetUtcNow().UtcDateTime - gone > HelperGrace)
            {
                _sessions.TryRemove(code, out _);
                return null;
            }
            s.Helper = helper;
            s.HelperGone = null;
        }
        return s;
    }

    // True when Martin did not come back within HelperGrace; the session is then removed.
    public bool HelperTimedOut(Session s)
    {
        lock (s)
        {
            if (s.HelperGone is not { } gone || clock.GetUtcNow().UtcDateTime - gone < HelperGrace) return false;
        }
        End(s);
        return true;
    }

    public void LeaveCustomer(Session s)
    {
        lock (s) { s.Customer = null; s.Created = clock.GetUtcNow().UtcDateTime; }
    }

    public int Count => _sessions.Count;

    public static async Task SendAsync(Session s, WebSocket? to, string json)
    {
        if (to is not { State: WebSocketState.Open }) return;
        await s.SendLock.WaitAsync();
        try { await to.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None); }
        catch (WebSocketException) { }
        finally { s.SendLock.Release(); }
    }
}
