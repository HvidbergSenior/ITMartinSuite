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
public sealed class GmailImap(string user, string appPassword, int maxMails) : IMailbox
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
        if ((client.Capabilities & ImapCapabilities.SpecialUse) != 0 && client.GetFolder(SpecialFolder.Sent) is { } sent)
        {
            await sent.OpenAsync(FolderAccess.ReadOnly, ct);
            var sentUids = await sent.SearchAsync(SearchQuery.DeliveredAfter(since.AddMonths(-12)), ct);
            foreach (var chunk in sentUids.Reverse().Take(5000).Chunk(500))
                foreach (var s in await sent.FetchAsync(chunk, MessageSummaryItems.Envelope, ct))
                    foreach (var a in s.Envelope.To.Mailboxes.Concat(s.Envelope.Cc.Mailboxes))
                        if (!string.Equals(a.Address, me, StringComparison.OrdinalIgnoreCase)) wroteTo.Add(a.Address.ToLowerInvariant());
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
        return new MailboxScan(mails, wroteTo);
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
            tabs.TryGetValue(s.UniqueId.Id, out var t) ? t : GmailTab.None);
    }
}

/// <summary>The follow-ups as one small JSON file (/data/opfoelgning.json): id, status, date, own note. Nothing else.</summary>
public sealed class JsonFollowUpStore(string path) : IFollowUpStore
{
    private readonly object _gate = new();
    private Dictionary<string, FollowUp>? _items;

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
        services.AddSingleton<IMailbox>(new GmailImap(user, appPassword, maxMails));
        services.AddSingleton<IFollowUpStore>(new JsonFollowUpStore(Path.Combine(dataDir, "opfoelgning.json")));
        return services;
    }
}
