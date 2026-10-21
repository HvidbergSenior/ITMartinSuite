using System.Collections.Concurrent;
using ITMartinHjem.Domain;
using Microsoft.Extensions.Logging;

namespace ITMartinHjem.Application;

// Web Push to the owner's own devices, and to a chat visitor who asked for it.
public sealed class PushUseCases(IPushSubscriptionRepository subs, IPushSender sender, ILogger<PushUseCases> log)
{
    public string PublicKey => sender.PublicKey;

    public Task SubscribeOwnerAsync(string endpoint, string p256dh, string auth, CancellationToken ct = default) =>
        subs.AddIfNewAsync(new PushSub { Endpoint = endpoint, P256dh = p256dh, Auth = auth }, ct);

    /// <returns>How many of Martin's devices took it. A subscription that is gone is removed (the phone must turn it on again).</returns>
    public async Task<int> SendToOwnerAsync(string title, string body, string url, CancellationToken ct = default)
    {
        var sent = 0;
        var gone = new List<int>();
        foreach (var s in await subs.AllAsync(ct))
        {
            switch (await sender.SendAsync(new PushTarget(s.Endpoint, s.P256dh, s.Auth), title, body, url, ct))
            {
                case PushResult.Sent: sent++; break;
                case PushResult.Gone: gone.Add(s.Id); log.LogWarning("Push subscription gone - removed; the phone must turn notifications on again"); break;
            }
        }
        if (gone.Count > 0) await subs.RemoveAsync(gone, ct);
        return sent;
    }
}

// Live chat between a visitor and the owner. Both sides are Blazor circuits in this one
// process, so a plain event is enough to update the other screen at once.
public sealed class ChatUseCases(IChatRepository threads, PushUseCases push, IPushSender pushSender, IMailSender mail, HjemOptions options, ILogger<ChatUseCases> log)
{
    public event Action<int>? Changed;            // thread id
    public event Action? AvailabilityChanged;

    public Task<ChatThread?> GetAsync(string visitorKey) => threads.ByVisitorAsync(visitorKey);
    public Task<ChatThread?> GetAsync(int id) => threads.ByIdAsync(id);
    public Task<List<ChatThread>> ThreadsAsync() => threads.NewestAsync(100);

    // Visitors with the chat open right now (ChatBox registers itself) - they see a reply at once,
    // so they get no mail or notification for it.
    private readonly ConcurrentDictionary<string, int> _online = new();
    public void VisitorOnline(string key) => _online.AddOrUpdate(key, 1, (_, n) => n + 1);
    public void VisitorOffline(string key) { if (_online.AddOrUpdate(key, 0, (_, n) => n - 1) <= 0) _online.TryRemove(key, out _); }

    public async Task SetVisitorEmailAsync(string visitorKey, string email)
    {
        email = email.Trim();
        if (ChatRules.LooksLikeEmail(email)) await threads.SetVisitorEmailAsync(visitorKey, email);
    }

    public async Task<bool> SetVisitorPushAsync(string visitorKey, string endpoint, string p256dh, string auth) =>
        ChatRules.ValidKey(visitorKey) && endpoint.StartsWith("https://")
        && await threads.SetVisitorPushAsync(visitorKey, $"{endpoint}|{p256dh}|{auth}");

    public async Task VisitorWritesAsync(string visitorKey, string name, string text, string email = "")
    {
        text = ChatRules.Trim(text);
        if (text.Length == 0 || !ChatRules.ValidKey(visitorKey)) return;
        name = ChatRules.Trim(name, ChatRules.MaxName);
        email = email.Trim();
        var newEmail = false;
        var t = await threads.ChangeForVisitorAsync(visitorKey, t =>
        {
            if (name.Length > 0) t.Name = name;
            newEmail = t.VisitorEmail.Length == 0 && ChatRules.LooksLikeEmail(email);
            if (newEmail) t.VisitorEmail = email;
            t.LastAt = DateTime.UtcNow;
            t.UnreadForOwner = true;
            t.Messages.Add(new ChatMessage { Text = text });
        });
        Changed?.Invoke(t.Id);

        if (newEmail) _ = Task.Run(() => ReceiptAsync(t.VisitorEmail, t.Name, t.IsPilot));

        try { await push.SendToOwnerAsync($"💬 {t.Who} skriver", ChatRules.Preview(text), $"/admin?chat={t.Id}"); }
        catch (Exception ex) { log.LogWarning(ex, "Chat push failed"); }
    }

    public async Task OwnerWritesAsync(int threadId, string text)
    {
        text = ChatRules.Trim(text);
        if (text.Length == 0) return;
        var t = await threads.ChangeAsync(threadId, t =>
        {
            t.LastAt = DateTime.UtcNow;
            t.UnreadForOwner = false;
            t.Messages.Add(new ChatMessage { FromOwner = true, Text = text });
            return Task.FromResult(true);
        });
        if (t is not null) Changed?.Invoke(t.Id);
    }

    // ── Tell the visitor that Martin answered, and mail Martin a copy of what is still unread (rules in ChatThread).
    // Found in the database by the sweep every 15 s, so a deploy loses nothing.

    // A mail that keeps failing is tried on 5 sweeps (about a minute), then given up and logged - never every 15 s forever.
    private const int MaxMailTries = 5;
    private readonly ConcurrentDictionary<int, int> _visitorFailures = new(), _ownerFailures = new();

    private bool GiveUp(ConcurrentDictionary<int, int> failures, int threadId, string what)
    {
        var n = failures.AddOrUpdate(threadId, 1, (_, x) => x + 1);
        if (n < MaxMailTries) return false;
        failures.TryRemove(threadId, out _);
        log.LogError("Gave up on the {What} for chat {Thread} after {Tries} failed tries", what, threadId, n);
        return true;
    }

    public async Task SweepAsync(DateTime nowUtc)
    {
        foreach (var id in await threads.ActiveSinceAsync(nowUtc - ChatRules.Recent))
        {
            if (await threads.ByIdAsync(id) is not { } t) continue;
            if (t.VisitorIsDue(nowUtc)) await NotifyVisitorAsync(id);
            if (t.OwnerCopyIsDue(nowUtc)) await OwnerCopyAsync(id);
        }
    }

    private const string Signature = "Venlig hilsen\nMartin Hvidberg · ITMartin\nITMartin@Mensa.dk · 31 19 47 30\n\n";

    // "Tak – jeg har fået din besked": once, when a visitor (chat or pilot form) gives an email.
    private async Task ReceiptAsync(string to, string name, bool pilot)
    {
        var hello = name.Length > 0 ? $"Hej {name.Replace(" (pilot)", "")}" : "Hej";
        var body = pilot
            ? $"{hello}\n\nTak for din henvendelse om at blive pilot hos ITMartin. Jeg har fået den og ringer eller skriver til dig inden for to dage.\n\n"
            : $"{hello}\n\nTak for din besked på itmartin.dk. Jeg har fået den, og du får en mail her, så snart jeg har svaret.\n\n";
        body += Signature + "Du får denne mail, fordi din e-mail blev skrevet på itmartin.dk. Var det ikke dig, så se bort fra den.";
        await mail.SendAsync(to, pilot ? "Tak for din pilot-henvendelse" : "Tak for din besked", body);
    }

    // Backup for Martin: a visitor message still unread in Svar after 2 minutes also comes by mail
    // (phone notifications can be off - they were on 2026-09-29).
    private async Task OwnerCopyAsync(int threadId)
    {
        try
        {
            await threads.ChangeAsync(threadId, async t =>
            {
                if (!t.UnreadForOwner) return false;   // already read in Svar
                var news = t.UnansweredFromVisitor();
                if (news.Count == 0) return false;
                var body = $"{t.Who} har skrevet på itmartin.dk og venter på svar:\n\n" +
                           string.Join("\n\n", news.Select(n => "  " + n.Replace("\n", "\n  "))) +
                           (t.VisitorEmail.Length > 0 ? $"\n\nVil have svar på mail: {t.VisitorEmail}" : "") +
                           $"\n\nSvar i Svar: {options.PublicUrl}/admin?chat={t.Id}";
                if (!await mail.SendAsync(options.OwnerEmail, $"💬 {t.Who} venter på svar", body)
                    && !GiveUp(_ownerFailures, threadId, "owner copy mail")) return false;
                _ownerFailures.TryRemove(threadId, out _);
                t.OwnerMailedAt = DateTime.UtcNow;
                return true;
            });
        }
        catch (Exception ex) { log.LogWarning(ex, "Owner copy mail failed"); }
    }

    private async Task NotifyVisitorAsync(int threadId)
    {
        try
        {
            await threads.ChangeAsync(threadId, async t =>
            {
                if (t.VisitorEmail.Length == 0 && t.VisitorPush.Length == 0) return false;
                if (_online.ContainsKey(t.VisitorKey)) { t.NotifiedAt = DateTime.UtcNow; return true; }
                var replies = t.UntoldReplies();
                if (replies.Count == 0) return false;
                var joined = string.Join("\n\n", replies);

                if (t.VisitorPush.Split('|') is [var ep, var p256, var auth] &&
                    await pushSender.SendAsync(new PushTarget(ep, p256, auth), "💬 Martin har svaret dig", ChatRules.Preview(joined), "/#chat") == PushResult.Gone)
                    t.VisitorPush = "";   // turned off in the browser

                if (t.VisitorEmail.Length > 0)
                {
                    var hello = t.Name.Length > 0 ? $"Hej {t.Name}" : "Hej";
                    var body =
                        $"{hello}\n\nMartin har svaret på din besked på itmartin.dk:\n\n" +
                        string.Join("\n\n", replies.Select(r => "  " + r.Replace("\n", "\n  "))) +
                        (t.IsPilot
                            ? "\n\nDu kan svare direkte på denne mail.\n\n"
                            : $"\n\nDu kan svare direkte på denne mail – eller fortsætte i chatten på {options.PublicUrl} (på den telefon eller PC, du skrev fra).\n\n") +
                        Signature + "Du får denne mail, fordi du skrev din e-mail i chatten. Vil du ikke have flere, så svar \"stop\".";
                    // Only count it as told when the mail really left (2026-10-08: a refused mail used to be marked as sent).
                    if (!await mail.SendAsync(t.VisitorEmail, "Martin har svaret dig", body) && !GiveUp(_visitorFailures, threadId, "reply mail to the visitor"))
                        return false;   // the next sweep tries again
                }
                _visitorFailures.TryRemove(threadId, out _);
                t.NotifiedAt = DateTime.UtcNow;
                return true;
            });
        }
        catch (Exception ex) { log.LogWarning(ex, "Telling the visitor about the reply failed"); }
    }

    public Task MarkReadAsync(int threadId) => threads.MarkReadAsync(threadId);

    public async Task DeleteAsync(int threadId)
    {
        await threads.DeleteAsync(threadId);
        Changed?.Invoke(threadId);
    }

    public void RaiseAvailability() => AvailabilityChanged?.Invoke();

    // ── The forms that become conversations in Svar ──

    /// <summary>The pilot form (wwwroot/bliv-pilot): a conversation in Svar instead of a mail, with a push to Martin.</summary>
    public Task PilotRequestAsync(string ydelse, string navn, string tlf, string by, string besked, string email) =>
        VisitorWritesAsync(ChatRules.PilotPrefix + Guid.NewGuid().ToString("N"), $"{navn} (pilot)",
            $"🐦 Pilot-henvendelse: {ydelse}\nTelefon: {tlf}\nBy: {by}" + (email.Length > 0 ? $"\nE-mail: {email}" : "") + $"\n\n{besked}", email);
}

// Application's IOwnerInbox = the chat in Svar: each "prøv gratis" request becomes its own conversation.
internal sealed class ChatOwnerInbox(ChatUseCases chat) : IOwnerInbox
{
    public Task NewRequestAsync(string name, string email, string text, CancellationToken ct = default) =>
        chat.VisitorWritesAsync(ChatRules.TrialPrefix + Guid.NewGuid().ToString("N"), name, text, email);
}
