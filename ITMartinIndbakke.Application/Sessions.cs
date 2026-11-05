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
public sealed class MailSessions(IMailAccessFactory mail, IAccountStores stores, ICustomerCheck customers, ILoggerFactory logs, TimeProvider clock)
{
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

        var account = MinIndbakkeRules.AccountKey(k);
        var ws = MailWorkspace.Create(mail.Open(email, appPassword), stores.FollowUps(account), stores.Log(account),
            name.Length > 0 ? name : email, logs, clock);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        Sweep();
        _open[token] = new Entry(ws, clock.GetUtcNow());
        ws.Scanner.Start(12);   // the overview is ready a minute after "Log ind"
        return new(token, null);
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
