using System.Collections.Concurrent;
using System.Security.Cryptography;
using ITMartinIndbakke.Domain;
using Microsoft.Extensions.Logging;

namespace ITMartinIndbakke.Application;

/// <summary>One person's way into their own mail, made from their key. Implemented in Infrastructure (Gmail over IMAP).</summary>
public sealed record MailAccess(IMailbox Mailbox, IMailboxCleaner Cleaner, IUnsubscriber Unsubscriber);

public interface IMailAccessFactory
{
    MailAccess Open(string email, string appPassword);
    /// <returns>Null when the mail said yes to the key, else a Danish line for the person.</returns>
    Task<string?> CheckLoginAsync(string email, string appPassword, CancellationToken ct);
}

/// <summary>Where one customer's own marks live (Klaret, frister, seneste oprydning) - never the key, never mail text.</summary>
public interface IAccountStores
{
    IFollowUpStore FollowUps(string account);
    ICleanupLog Log(string account);
}

/// <summary>"Log ind med Microsoft" for Outlook.com/Hotmail (2026-11-05): the person signs in at Microsoft and says Tillad;
/// we get a short-lived ticket (OAuth), never a password. Implemented in Infrastructure.</summary>
public interface IMicrosoftLogin
{
    bool IsConfigured { get; }
    string AuthorizeUrl(string state, string codeChallenge);
    /// <returns>The mail address + a way into it, or a Danish error.</returns>
    Task<(string? Email, MailAccess? Access, string? Error)> FinishAsync(string authCode, string codeVerifier, CancellationToken ct);
}

/// <summary>Is this a real kundenummer that Martin has given Min Indbakke? (Kunder + Indbakke__Kunder.)</summary>
public interface ICustomerCheck
{
    Task<(bool Ok, string Name, string Error)> CheckAsync(string code, CancellationToken ct);
}

/// <summary>Everything one mailbox needs: the scan, Det haster and Ryd op - the same use cases Martin's own Indbakke uses.</summary>
public sealed class MailWorkspace(MailScanner scanner, FollowUps followUps, Cleanup cleanup, string name)
{
    public MailScanner Scanner { get; } = scanner;
    public FollowUps FollowUps { get; } = followUps;
    public Cleanup Cleanup { get; } = cleanup;
    public string Name { get; } = name;

    public static MailWorkspace Create(MailAccess access, IFollowUpStore store, ICleanupLog log, string name, ILoggerFactory logs, TimeProvider clock)
    {
        var scanner = new MailScanner(access.Mailbox, logs.CreateLogger<MailScanner>(), clock);
        return new(scanner, new FollowUps(store, scanner, clock), new Cleanup(scanner, access.Cleaner, access.Unsubscriber, log, clock), name);
    }
}

/// <summary>
/// Min Indbakke logins. The key is held only in memory, inside the person's workspace, and dropped after
/// <see cref="MinIndbakkeRules.IdleTimeout"/> without a request, or at Log ud. Nothing about the key is ever saved.
/// </summary>
public sealed class MailSessions(IMailAccessFactory mail, IAccountStores stores, ICustomerCheck customers, ILoggerFactory logs, TimeProvider clock,
    IMicrosoftLogin? microsoft = null)
{
    // A Microsoft sign-in in progress: our state -> (number, name, PKCE verifier). Gone after 10 minutes or when used.
    private sealed record Pending(string Code, string Name, string Verifier, DateTimeOffset At);
    private readonly ConcurrentDictionary<string, Pending> _pending = new();

    public bool MicrosoftReady => microsoft?.IsConfigured == true;

    /// <summary>Step 1: the number is checked first, then the person goes to Microsoft.</summary>
    public async Task<(string? Url, string? Error)> BeginMicrosoftAsync(string? code, CancellationToken ct = default)
    {
        if (microsoft is not { IsConfigured: true }) return (null, "Log ind med Microsoft er ikke slået til endnu.");
        var k = MinIndbakkeRules.NormalizeCode(code);
        if (k.Length != 8) return (null, "Dit nummer har 8 cifre – du finder det i mailen fra Martin.");
        var (ok, name, error) = await customers.CheckAsync(k, ct);
        if (!ok) return (null, error);
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)));
        foreach (var (s, p) in _pending) if (clock.GetUtcNow() - p.At > TimeSpan.FromMinutes(10)) _pending.TryRemove(s, out _);
        _pending[state] = new Pending(k, name, verifier, clock.GetUtcNow());
        return (microsoft.AuthorizeUrl(state, challenge), null);
    }

    /// <summary>Step 2: back from Microsoft with a one-time code - the session is made like a Gmail login.</summary>
    public async Task<Login> FinishMicrosoftAsync(string? state, string? authCode, CancellationToken ct = default)
    {
        if (microsoft is null || string.IsNullOrEmpty(state) || !_pending.TryRemove(state, out var p)
            || clock.GetUtcNow() - p.At > TimeSpan.FromMinutes(10))
            return new(null, "Login med Microsoft tog for lang tid. Prøv igen.");
        if (string.IsNullOrEmpty(authCode)) return new(null, "Microsoft sagde nej – du trykkede måske Annuller. Prøv igen.");
        var (email, access, error) = await microsoft.FinishAsync(authCode, p.Verifier, ct);
        if (access is null) return new(null, error ?? "Microsoft-login virkede ikke. Prøv igen.");
        return Start(p.Code, access, p.Name.Length > 0 ? p.Name : email ?? "");
    }

    private Login Start(string code, MailAccess access, string name)
    {
        var account = MinIndbakkeRules.AccountKey(code);
        var ws = MailWorkspace.Create(access, stores.FollowUps(account), stores.Log(account), name, logs, clock);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        Sweep();
        _open[token] = new Entry(ws, clock.GetUtcNow());
        ws.Scanner.Start(12);
        return new(token, null);
    }

    private static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class Entry(MailWorkspace workspace, DateTimeOffset seen) { public MailWorkspace Workspace = workspace; public DateTimeOffset Seen = seen; }
    private readonly ConcurrentDictionary<string, Entry> _open = new();

    public sealed record Login(string? Token, string? Error);

    public async Task<Login> OpenAsync(string? code, string? email, string? appPassword, CancellationToken ct = default)
    {
        var k = MinIndbakkeRules.NormalizeCode(code);
        email = (email ?? "").Trim();
        appPassword = (appPassword ?? "").Replace(" ", "");
        if (MinIndbakkeRules.CheckInput(k, email, appPassword) is { } bad) return new(null, bad);

        var (ok, name, error) = await customers.CheckAsync(k, ct);
        if (!ok) return new(null, error);
        if (await mail.CheckLoginAsync(email, appPassword, ct) is { } no) return new(null, no);

        return Start(k, mail.Open(email, appPassword), name.Length > 0 ? name : email);   // the overview is ready a minute after "Log ind"
    }

    /// <summary>The person's workspace while they use the page; each request keeps it alive.</summary>
    public MailWorkspace? Get(string? token)
    {
        Sweep();
        if (string.IsNullOrEmpty(token) || !_open.TryGetValue(token, out var e)) return null;
        e.Seen = clock.GetUtcNow();
        return e.Workspace;
    }

    public void Close(string? token) { if (!string.IsNullOrEmpty(token)) _open.TryRemove(token, out _); }

    public int OpenCount { get { Sweep(); return _open.Count; } }

    private void Sweep()
    {
        var now = clock.GetUtcNow();
        foreach (var (t, e) in _open)
            if (now - e.Seen > MinIndbakkeRules.IdleTimeout) _open.TryRemove(t, out _);
    }
}
