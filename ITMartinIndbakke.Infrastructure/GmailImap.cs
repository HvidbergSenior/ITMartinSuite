using System.Text.Json;
using ITMartinIndbakke.Application;
using ITMartinIndbakke.Domain;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinIndbakke.Infrastructure;

/// <summary>
/// Gmail over IMAP with an app password. READ-ONLY: the folders are opened ReadOnly, so not even "read" flags change.
/// Only headers are fetched (Envelope + flags + the list headers) - never the text of a mail.
/// Gmail's tabs come from Gmail searches (X-GM-RAW "category:promotions"), which also read nothing.
/// </summary>
public sealed class GmailImap(string user, string appPassword, int maxMails) : IMailbox, IMailboxCleaner
{
    private static readonly string[] ListHeaders = ["List-Unsubscribe", "List-Unsubscribe-Post", "List-Id", "Precedence", "Auto-Submitted"];

    public bool IsConfigured => user.Contains('@') && appPassword.Replace(" ", "").Length >= 16;

    public async Task<MailboxScan> ScanAsync(int months, IProgress<(int Done, int Total)> progress, CancellationToken ct)
    {
        using var client = new ImapClient { Timeout = 120_000 };
        await client.ConnectAsync("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect, ct);
        await client.AuthenticateAsync(user, appPassword.Replace(" ", ""), ct);
        var since = DateTime.UtcNow.Date.AddMonths(-months);
        var me = user.ToLowerInvariant();

        // Who I have written to - "Sendt post" (found by its special-use flag, so the Danish/English name does not matter).
        var wroteTo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mine = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { me };   // + every From in Sendt post = my aliases
        if ((client.Capabilities & ImapCapabilities.SpecialUse) != 0 && client.GetFolder(SpecialFolder.Sent) is { } sent)
        {
            await sent.OpenAsync(FolderAccess.ReadOnly, ct);
            var sentUids = await sent.SearchAsync(SearchQuery.DeliveredAfter(since.AddMonths(-12)), ct);
            foreach (var chunk in sentUids.Reverse().Take(5000).Chunk(500))
                foreach (var s in await sent.FetchAsync(chunk, MessageSummaryItems.Envelope, ct))
                {
                    foreach (var f in s.Envelope.From.Mailboxes) mine.Add(f.Address.ToLowerInvariant());
                    foreach (var a in s.Envelope.To.Mailboxes.Concat(s.Envelope.Cc.Mailboxes))
                        if (!string.Equals(a.Address, me, StringComparison.OrdinalIgnoreCase)) wroteTo.Add(a.Address.ToLowerInvariant());
                }
            await sent.CloseAsync(false, ct);
        }

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, ct);
        var after = SearchQuery.DeliveredAfter(since);
        var uids = (await inbox.SearchAsync(after, ct)).Reverse().Take(maxMails).ToList();   // newest first, hard cap

        var tabs = new Dictionary<uint, GmailTab>();
        foreach (var (tab, raw) in new[] { (GmailTab.Promotions, "promotions"), (GmailTab.Social, "social"), (GmailTab.Updates, "updates"), (GmailTab.Forums, "forums") })
            foreach (var uid in await inbox.SearchAsync(after.And(SearchQuery.GMailRawSearch("category:" + raw)), ct))
                tabs.TryAdd(uid.Id, tab);

        var request = new FetchRequest(MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.Flags)
        {
            Headers = new HeaderSet(ListHeaders),
        };
        var mails = new List<MailHeader>(uids.Count);
        progress.Report((0, uids.Count));
        foreach (var chunk in uids.Chunk(500))
        {
            foreach (var s in await inbox.FetchAsync(chunk, request, ct))
                mails.Add(ToHeader(s, me, tabs));
            progress.Report((mails.Count, uids.Count));
        }
        await inbox.CloseAsync(false, ct);
        await client.DisconnectAsync(true, ct);
        wroteTo.ExceptWith(mine);
        return new MailboxScan(mails, wroteTo, mine);
    }

    private async Task<ImapClient> ConnectAsync(CancellationToken ct)
    {
        var client = new ImapClient { Timeout = 600_000 };   // moves of thousands: Gmail is slow per batch (01:32 run timed out at 2 min but Gmail moved all 21,141)
        await client.ConnectAsync("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect, ct);
        await client.AuthenticateAsync(user, appPassword.Replace(" ", ""), ct);
        return client;
    }

    private static IMailFolder Trash(ImapClient client) =>
        client.GetFolder(SpecialFolder.Trash) ?? throw new InvalidOperationException("Gmail har ingen papirkurv-mappe.");

    /// <summary>v2: inbox -> Papirkurv (Gmail keeps it 30 days). The ONLY write besides Restore. Hard cap per call.</summary>
    public async Task<int> MoveToTrashAsync(IReadOnlyList<uint> uids, CancellationToken ct)
    {
        if (uids.Count == 0) return 0;
        if (uids.Count > CleanupRules.MaxPerAction) throw new InvalidOperationException("For mange mails på én gang.");
        using var client = await ConnectAsync(ct);
        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadWrite, ct);
        var trash = Trash(client);
        var moved = 0;
        foreach (var chunk in uids.Select(u => new UniqueId(u)).Chunk(500))
        {
            var map = await inbox.MoveToAsync(chunk, trash, ct);
            moved += map?.Source.Count ?? chunk.Length;
        }
        await client.DisconnectAsync(true, ct);
        return moved;
    }

    public async Task<(int Total, int Trash, int Starred)> CountOldAsync(DateOnly before, OldMode mode, CancellationToken ct)
    {
        using var client = await ConnectAsync(ct);
        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, ct);
        var total = (await inbox.SearchAsync(SearchQuery.GMailRawSearch(OldCleanupRules.Before(before)), ct)).Count;
        var trash = (await inbox.SearchAsync(SearchQuery.GMailRawSearch(OldCleanupRules.TrashQuery(before, mode)), ct)).Count;
        var starred = (await inbox.SearchAsync(SearchQuery.GMailRawSearch(OldCleanupRules.Before(before) + " is:starred"), ct)).Count;
        await client.DisconnectAsync(true, ct);
        return (total, trash, starred);
    }

    public async Task<(int Trashed, int Archived)> ClearOldAsync(DateOnly before, OldMode mode, string label, CancellationToken ct)
    {
        using var client = await ConnectAsync(ct);
        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadWrite, ct);
        var all = await inbox.SearchAsync(SearchQuery.GMailRawSearch(OldCleanupRules.Before(before) + " -is:starred"), ct);
        var toTrash = (await inbox.SearchAsync(SearchQuery.GMailRawSearch(OldCleanupRules.TrashQuery(before, mode)), ct)).ToHashSet();
        var toArchive = mode == OldMode.SletAlt ? new List<UniqueId>() : all.Where(u => !toTrash.Contains(u)).ToList();

        // 1. the label first - so Fortryd can always find the whole pile, wherever it went
        foreach (var chunk in all.Chunk(200))
            await inbox.StoreAsync(chunk, new StoreLabelsRequest(StoreAction.Add, new[] { label }) { Silent = true }, ct);
        // 2. junk (or everything) -> Papirkurv
        var trashFolder = Trash(client);
        foreach (var chunk in toTrash.Chunk(200)) await inbox.MoveToAsync(chunk, trashFolder, ct);
        // 3. the rest -> Arkiv (Gmail: out of Indbakke, still under "Alle mails")
        if (toArchive.Count > 0)
        {
            var archive = client.GetFolder(SpecialFolder.All) ?? throw new InvalidOperationException("Gmail har ingen 'Alle mails'-mappe.");
            foreach (var chunk in toArchive.Chunk(200)) await inbox.MoveToAsync(chunk, archive, ct);
        }
        await client.DisconnectAsync(true, ct);
        return (toTrash.Count, toArchive.Count);
    }

    public async Task<int> RestoreLabelAsync(string label, CancellationToken ct)
    {
        using var client = await ConnectAsync(ct);
        var back = 0;
        var trash = Trash(client);
        await trash.OpenAsync(FolderAccess.ReadWrite, ct);
        var fromTrash = await trash.SearchAsync(SearchQuery.HasGMailLabel(label), ct);
        foreach (var chunk in fromTrash.Chunk(200)) await trash.MoveToAsync(chunk, client.Inbox, ct);
        back += fromTrash.Count;
        await trash.CloseAsync(false, ct);
        if (client.GetFolder(SpecialFolder.All) is { } archive)
        {
            await archive.OpenAsync(FolderAccess.ReadWrite, ct);
            var archived = await archive.SearchAsync(SearchQuery.GMailRawSearch("label:" + label + " -in:inbox"), ct);
            // Gmail: a copy into INBOX = the Indbakke label back on the same mail (no duplicate)
            foreach (var chunk in archived.Chunk(200)) await archive.CopyToAsync(chunk, client.Inbox, ct);
            back += archived.Count;
        }
        await client.DisconnectAsync(true, ct);
        return back;
    }

    /// <summary>Fortryd: Papirkurv -> Indbakke, found by Message-ID (UIDs change when a mail moves).</summary>
    public async Task<int> RestoreAsync(IReadOnlyList<string> messageIds, CancellationToken ct)
    {
        if (messageIds.Count == 0) return 0;
        using var client = await ConnectAsync(ct);
        var trash = Trash(client);
        await trash.OpenAsync(FolderAccess.ReadWrite, ct);
        var back = 0;
        foreach (var chunk in messageIds.Where(id => !id.StartsWith("uid-")).Chunk(25))
        {
            SearchQuery q = SearchQuery.HeaderContains("Message-ID", chunk[0]);
            foreach (var id in chunk.Skip(1)) q = q.Or(SearchQuery.HeaderContains("Message-ID", id));
            var found = await trash.SearchAsync(q, ct);
            if (found.Count == 0) continue;
            await trash.MoveToAsync(found, client.Inbox, ct);
            back += found.Count;
        }
        await client.DisconnectAsync(true, ct);
        return back;
    }

    private static MailHeader ToHeader(IMessageSummary s, string me, Dictionary<uint, GmailTab> tabs)
    {
        var env = s.Envelope;
        var from = env.From.Mailboxes.FirstOrDefault();
        var h = s.Headers;
        var unsubscribe = h?["List-Unsubscribe"];
        var precedence = (h?["Precedence"] ?? "").ToLowerInvariant();
        var auto = (h?["Auto-Submitted"] ?? "").ToLowerInvariant();
        var flags = s.Flags ?? MessageFlags.None;
        return new MailHeader(
            s.UniqueId.Id,
            env.MessageId ?? $"uid-{s.UniqueId.Id}",
            from?.Address ?? "",
            from?.Name ?? "",
            env.Subject ?? "",
            env.Date ?? s.InternalDate ?? DateTimeOffset.MinValue,
            flags.HasFlag(MessageFlags.Seen),
            flags.HasFlag(MessageFlags.Answered),
            env.To.Mailboxes.Any(m => string.Equals(m.Address, me, StringComparison.OrdinalIgnoreCase)),
            string.IsNullOrWhiteSpace(unsubscribe) ? null : unsubscribe.Trim(),
            (h?["List-Unsubscribe-Post"] ?? "").Contains("One-Click", StringComparison.OrdinalIgnoreCase),
            !string.IsNullOrWhiteSpace(h?["List-Id"]) || precedence is "bulk" or "list",
            auto.Length > 0 && auto != "no",
            tabs.TryGetValue(s.UniqueId.Id, out var t) ? t : GmailTab.None,
            flags.HasFlag(MessageFlags.Flagged));
    }
}

/// <summary>The follow-ups as one small JSON file (/data/opfoelgning.json): id, status, date, own note. Nothing else.</summary>
public sealed class JsonFollowUpStore(string path) : IFollowUpStore
{
    private readonly object _gate = new();
    private Dictionary<string, FollowUp>? _items;

    private string BaselinePath => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, "grundlinje.txt");

    /// <summary>The base line as one ISO date in grundlinje.txt; null = no line (the old file is kept as .fortrudt).</summary>
    public DateTimeOffset? Baseline
    {
        get
        {
            lock (_gate)
                return File.Exists(BaselinePath) && DateTimeOffset.TryParse(File.ReadAllText(BaselinePath).Trim(), out var d) ? d : null;
        }
        set
        {
            lock (_gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BaselinePath)!);
                if (value is { } v) File.WriteAllText(BaselinePath, v.ToString("O"));
                else if (File.Exists(BaselinePath)) File.Move(BaselinePath, BaselinePath + ".fortrudt", overwrite: true);
            }
        }
    }

    public IReadOnlyDictionary<string, FollowUp> All()
    {
        lock (_gate) return new Dictionary<string, FollowUp>(Load());
    }

    public void Save(FollowUp followUp)
    {
        lock (_gate)
        {
            var items = Load();
            items[followUp.MessageId] = followUp;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(items.Values));
            File.Move(tmp, path, overwrite: true);
        }
    }

    private Dictionary<string, FollowUp> Load()
    {
        if (_items is not null) return _items;
        _items = File.Exists(path)
            ? (JsonSerializer.Deserialize<List<FollowUp>>(File.ReadAllText(path)) ?? []).ToDictionary(f => f.MessageId)
            : [];
        return _items;
    }
}

public static class IndbakkeInfrastructure
{
    public static IServiceCollection AddIndbakkeInfrastructure(this IServiceCollection services, string user, string appPassword, string dataDir, int maxMails = 20000)
    {
        var gmail = new GmailImap(user, appPassword, maxMails);
        services.AddSingleton<IMailbox>(gmail);
        services.AddSingleton<IMailboxCleaner>(gmail);
        services.AddHttpClient("unsubscribe", c => { c.Timeout = TimeSpan.FromSeconds(20); c.DefaultRequestHeaders.UserAgent.ParseAdd("Kolibri-Indbakke/1.0"); });
        services.AddSingleton<IUnsubscriber>(sp => new Unsubscriber(sp.GetRequiredService<IHttpClientFactory>().CreateClient("unsubscribe"), user, appPassword));
        services.AddSingleton<ICleanupLog>(new JsonCleanupLog(Path.Combine(dataDir, "oprydning.json")));
        services.AddSingleton<IFollowUpStore>(new JsonFollowUpStore(Path.Combine(dataDir, "opfoelgning.json")));
        return services;
    }
}
