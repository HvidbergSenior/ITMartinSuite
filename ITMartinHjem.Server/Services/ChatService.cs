using ITMartinHjem.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ITMartinHjem.Server.Services;

// Live chat between a visitor and the owner. Both sides are Blazor circuits in this one
// process, so a plain event is enough to update the other screen at once.
public sealed class ChatService(IDbContextFactory<HjemDb> dbf, PushService push, MailService mail, IConfiguration cfg, ILogger<ChatService> log)
{
    public event Action<int>? Changed;            // thread id
    public event Action? AvailabilityChanged;

    public const int MaxText = 2000;

    public async Task<ChatThread?> GetAsync(string visitorKey)
    {
        await using var db = await dbf.CreateDbContextAsync();
        return await db.Threads.Include(t => t.Messages.OrderBy(m => m.At)).AsNoTracking()
            .FirstOrDefaultAsync(t => t.VisitorKey == visitorKey);
    }

    public async Task<ChatThread?> GetAsync(int id)
    {
        await using var db = await dbf.CreateDbContextAsync();
        return await db.Threads.Include(t => t.Messages.OrderBy(m => m.At)).AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<List<ChatThread>> ThreadsAsync()
    {
        await using var db = await dbf.CreateDbContextAsync();
        return await db.Threads.Include(t => t.Messages).AsNoTracking()
            .OrderByDescending(t => t.LastAt).Take(100).ToListAsync();
    }

    // Visitors with the chat open right now (ChatBox registers itself) - they see a reply at once,
    // so they get no mail or notification for it.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _online = new();
    public void VisitorOnline(string key) => _online.AddOrUpdate(key, 1, (_, n) => n + 1);
    public void VisitorOffline(string key) { if (_online.AddOrUpdate(key, 0, (_, n) => n - 1) <= 0) _online.TryRemove(key, out _); }

    public async Task SetVisitorEmailAsync(string visitorKey, string email)
    {
        email = email.Trim();
        if (!MailService.LooksLikeEmail(email)) return;
        await using var db = await dbf.CreateDbContextAsync();
        await db.Threads.Where(t => t.VisitorKey == visitorKey).ExecuteUpdateAsync(s => s.SetProperty(t => t.VisitorEmail, email));
    }

    public async Task<bool> SetVisitorPushAsync(string visitorKey, string endpoint, string p256dh, string auth)
    {
        if (visitorKey.Length is < 8 or > 64 || !endpoint.StartsWith("https://")) return false;
        await using var db = await dbf.CreateDbContextAsync();
        return await db.Threads.Where(t => t.VisitorKey == visitorKey)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.VisitorPush, $"{endpoint}|{p256dh}|{auth}")) > 0;
    }

    public async Task VisitorWritesAsync(string visitorKey, string name, string text, string email = "")
    {
        text = Trim(text);
        if (text.Length == 0 || visitorKey.Length is < 8 or > 64) return;
        name = Trim(name, 60);
        await using var db = await dbf.CreateDbContextAsync();
        var t = await db.Threads.FirstOrDefaultAsync(x => x.VisitorKey == visitorKey);
        if (t is null) { t = new ChatThread { VisitorKey = visitorKey }; db.Threads.Add(t); }
        if (name.Length > 0) t.Name = name;
        var newEmail = t.VisitorEmail.Length == 0 && MailService.LooksLikeEmail(email.Trim());
        if (newEmail) t.VisitorEmail = email.Trim();
        t.LastAt = DateTime.UtcNow;
        t.UnreadForOwner = true;
        t.Messages.Add(new ChatMessage { Text = text });
        await db.SaveChangesAsync();
        Changed?.Invoke(t.Id);

        if (newEmail) _ = Task.Run(() => ReceiptAsync(t.VisitorEmail, t.Name, pilot: visitorKey.StartsWith("pilot-")));

        var who = t.Name.Length > 0 ? t.Name : "En besøgende";
        try { await push.SendToOwnerAsync($"💬 {who} skriver", text.Length > 120 ? text[..120] + "…" : text, $"/admin?chat={t.Id}"); }
        catch (Exception ex) { log.LogWarning(ex, "Chat push failed"); }
    }

    public async Task OwnerWritesAsync(int threadId, string text)
    {
        text = Trim(text);
        if (text.Length == 0) return;
        await using var db = await dbf.CreateDbContextAsync();
        var t = await db.Threads.FirstOrDefaultAsync(x => x.Id == threadId);
        if (t is null) return;
        t.LastAt = DateTime.UtcNow;
        t.UnreadForOwner = false;
        db.Messages.Add(new ChatMessage { ThreadId = t.Id, FromOwner = true, Text = text });
        await db.SaveChangesAsync();
        Changed?.Invoke(t.Id);
    }

    // ── Tell the visitor that Martin answered: 90 s after his LAST reply (so several quick replies become one mail),
    // by mail and/or browser notification - unless the visitor has the chat open and already sees it. And mail Martin a
    // copy when a visitor's message is still unread in Svar after 2 minutes. Both are found in the DATABASE by
    // ChatSweeper every 15 s (2026-10-08): before, they were in-memory timers, and a deploy within 90 s lost the mail.
    public static readonly TimeSpan VisitorDelay = TimeSpan.FromSeconds(90);
    public static readonly TimeSpan OwnerDelay = TimeSpan.FromMinutes(2);
    // Only recent messages count, so old conversations never wake up and mail after a deploy.
    private static readonly TimeSpan Recent = TimeSpan.FromHours(24);

    // A mail that keeps failing is tried on 5 sweeps (about a minute), then given up and logged - never every 15 s forever.
    private const int MaxMailTries = 5;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, int> _visitorFailures = new(), _ownerFailures = new();

    private bool GiveUp(System.Collections.Concurrent.ConcurrentDictionary<int, int> failures, int threadId, string what)
    {
        var n = failures.AddOrUpdate(threadId, 1, (_, x) => x + 1);
        if (n < MaxMailTries) return false;
        failures.TryRemove(threadId, out _);
        log.LogError("Gave up on the {What} for chat {Thread} after {Tries} failed tries", what, threadId, n);
        return true;
    }

    public async Task SweepAsync(DateTime nowUtc)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var since = nowUtc - Recent;
        var ids = await db.Threads.Where(t => t.LastAt > since).Select(t => t.Id).ToListAsync();
        foreach (var id in ids)
        {
            var t = await db.Threads.Include(x => x.Messages).AsNoTracking().FirstAsync(x => x.Id == id);
            var lastOwner = t.Messages.Where(m => m.FromOwner).Select(m => (DateTime?)m.At).Max();
            if (lastOwner is { } o && o > since && o > (t.NotifiedAt ?? DateTime.MinValue) && nowUtc - o >= VisitorDelay
                && (t.VisitorEmail.Length > 0 || t.VisitorPush.Length > 0))
                await NotifyVisitorAsync(id);
            var lastVisitor = t.Messages.Where(m => !m.FromOwner).Select(m => (DateTime?)m.At).Max();
            if (t.UnreadForOwner && lastVisitor is { } v && v > since && v > (t.OwnerMailedAt ?? DateTime.MinValue) && nowUtc - v >= OwnerDelay)
                await OwnerCopyAsync(id);
        }
    }

    // "Tak – jeg har fået din besked": once, when a visitor (chat or pilot form) gives an email.
    private async Task ReceiptAsync(string to, string name, bool pilot)
    {
        var hello = name.Length > 0 ? $"Hej {name.Replace(" (pilot)", "")}" : "Hej";
        var body = pilot
            ? $"{hello}\n\nTak for din henvendelse om at blive pilot hos ITMartin. Jeg har fået den og ringer eller skriver til dig inden for to dage.\n\n"
            : $"{hello}\n\nTak for din besked på itmartin.dk. Jeg har fået den, og du får en mail her, så snart jeg har svaret.\n\n";
        body += "Venlig hilsen\nMartin Hvidberg · ITMartin\nITMartin@Mensa.dk · 31 19 47 30\n\n" +
                "Du får denne mail, fordi din e-mail blev skrevet på itmartin.dk. Var det ikke dig, så se bort fra den.";
        await mail.SendAsync(to, pilot ? "Tak for din pilot-henvendelse" : "Tak for din besked", body);
    }

    // Backup for Martin: a visitor message still unread in Svar after 2 minutes also comes by mail
    // (phone notifications can be off - they were on 2026-09-29).
    private async Task OwnerCopyAsync(int threadId)
    {
        try
        {
            await using var db = await dbf.CreateDbContextAsync();
            var t = await db.Threads.Include(x => x.Messages).FirstOrDefaultAsync(x => x.Id == threadId);
            if (t is null || !t.UnreadForOwner) return;   // already read in Svar
            var lastOwner = t.Messages.Where(m => m.FromOwner).Select(m => m.At).DefaultIfEmpty(DateTime.MinValue).Max();
            var news = t.Messages.Where(m => !m.FromOwner && m.At > lastOwner).OrderBy(m => m.At).Select(m => m.Text).ToList();
            if (news.Count == 0) return;
            var who = t.Name.Length > 0 ? t.Name : "En besøgende";
            var site = cfg["Hjem:PublicUrl"] ?? "https://itmartin.dk";
            var body = $"{who} har skrevet på itmartin.dk og venter på svar:\n\n" +
                       string.Join("\n\n", news.Select(n => "  " + n.Replace("\n", "\n  "))) +
                       (t.VisitorEmail.Length > 0 ? $"\n\nVil have svar på mail: {t.VisitorEmail}" : "") +
                       $"\n\nSvar i Svar: {site}/admin?chat={t.Id}";
            if (!await mail.SendAsync(cfg["Hjem:OwnerEmail"] ?? "ITMartin@Mensa.dk", $"💬 {who} venter på svar", body)
                && !GiveUp(_ownerFailures, threadId, "owner copy mail")) return;
            _ownerFailures.TryRemove(threadId, out _);
            t.OwnerMailedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { log.LogWarning(ex, "Owner copy mail failed"); }
    }

    private async Task NotifyVisitorAsync(int threadId)
    {
        try
        {
            await using var db = await dbf.CreateDbContextAsync();
            var t = await db.Threads.Include(x => x.Messages).FirstOrDefaultAsync(x => x.Id == threadId);
            if (t is null || (t.VisitorEmail.Length == 0 && t.VisitorPush.Length == 0)) return;
            if (_online.ContainsKey(t.VisitorKey)) { t.NotifiedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return; }
            var since = t.NotifiedAt ?? DateTime.MinValue;
            var replies = t.Messages.Where(m => m.FromOwner && m.At > since).OrderBy(m => m.At).Select(m => m.Text).ToList();
            if (replies.Count == 0) return;
            var site = cfg["Hjem:PublicUrl"] ?? "https://itmartin.dk";
            var joined = string.Join("\n\n", replies);

            if (t.VisitorPush.Split('|') is [var ep, var p256, var auth] &&
                !await push.SendToAsync(ep, p256, auth, "💬 Martin har svaret dig", joined.Length > 120 ? joined[..120] + "…" : joined, "/#chat"))
                t.VisitorPush = "";   // turned off in the browser

            if (t.VisitorEmail.Length > 0)
            {
                var hello = t.Name.Length > 0 ? $"Hej {t.Name}" : "Hej";
                var body =
                    $"{hello}\n\nMartin har svaret på din besked på itmartin.dk:\n\n" +
                    string.Join("\n\n", replies.Select(r => "  " + r.Replace("\n", "\n  "))) +
                    (t.VisitorKey.StartsWith("pilot-")
                        ? "\n\nDu kan svare direkte på denne mail.\n\n"
                        : $"\n\nDu kan svare direkte på denne mail – eller fortsætte i chatten på {site} (på den telefon eller PC, du skrev fra).\n\n") +
                    "Venlig hilsen\nMartin Hvidberg · ITMartin\nITMartin@Mensa.dk · 31 19 47 30\n\n" +
                    "Du får denne mail, fordi du skrev din e-mail i chatten. Vil du ikke have flere, så svar \"stop\".";
                // Only count it as told when the mail really left (2026-10-08: a refused mail used to be marked as sent).
                if (!await mail.SendAsync(t.VisitorEmail, "Martin har svaret dig", body) && !GiveUp(_visitorFailures, threadId, "reply mail to the visitor"))
                    return;   // the next sweep tries again
            }
            _visitorFailures.TryRemove(threadId, out _);
            t.NotifiedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { log.LogWarning(ex, "Telling the visitor about the reply failed"); }
    }

    public async Task MarkReadAsync(int threadId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        await db.Threads.Where(t => t.Id == threadId).ExecuteUpdateAsync(s => s.SetProperty(t => t.UnreadForOwner, false));
    }

    public async Task DeleteAsync(int threadId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        await db.Threads.Where(t => t.Id == threadId).ExecuteDeleteAsync();
        Changed?.Invoke(threadId);
    }

    public void RaiseAvailability() => AvailabilityChanged?.Invoke();

    private static string Trim(string s, int max = MaxText) { s = (s ?? "").Trim(); return s.Length > max ? s[..max] : s; }
}
