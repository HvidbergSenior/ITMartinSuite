using ITMartinIndbakke.Application;
using ITMartinIndbakke.Domain;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;

namespace ITMartinIndbakke.Infrastructure;

/// <summary>
/// Outlook.com / Hotmail / Live over IMAP with a Microsoft sign-in (OAuth, 2026-11-05 "build outlook"). Same rules as Gmail:
/// headers only, never the text; scans are ReadOnly; the only writes are "to Slettet post" and Fortryd.
/// Outlook has no Gmail tabs, labels or conversation numbers: "Venter på dit svar" uses "I wrote to this person after it",
/// and "Ryd alt det gamle" is Gmail-only for now (it needs labels for Fortryd).
/// </summary>
public sealed class OutlookImap(string email, Func<CancellationToken, Task<string>> accessToken, int maxMails = 20000) : IMailbox, IMailboxCleaner
{
    public bool IsConfigured => email.Contains('@');
    public bool CanClearOld => false;

    private async Task<ImapClient> ConnectAsync(CancellationToken ct, int timeout = 120_000)
    {
        var client = new ImapClient { Timeout = timeout };
        await client.ConnectAsync("outlook.office365.com", 993, SecureSocketOptions.SslOnConnect, ct);
        await client.AuthenticateAsync(new SaslMechanismOAuth2(email, await accessToken(ct)), ct);
        return client;
    }

    /// <summary>Special-use flag first; else the English/Danish names Outlook uses.</summary>
    private static async Task<IMailFolder?> FolderAsync(ImapClient client, SpecialFolder special, string[] names, CancellationToken ct)
    {
        if ((client.Capabilities & ImapCapabilities.SpecialUse) != 0 && client.GetFolder(special) is { } f) return f;
        var root = client.GetFolder(client.PersonalNamespaces[0]);
        foreach (var sub in await root.GetSubfoldersAsync(false, ct))
            if (names.Any(n => string.Equals(sub.Name, n, StringComparison.OrdinalIgnoreCase))) return sub;
        return null;
    }

    private static readonly string[] SentNames = ["Sent", "Sent Items", "Sendt post", "Sendte elementer"];
    private static readonly string[] TrashNames = ["Deleted", "Deleted Items", "Slettet post", "Slettede elementer"];

    public async Task<MailboxScan> ScanAsync(int months, IProgress<(int Done, int Total)> progress, CancellationToken ct)
    {
        using var client = await ConnectAsync(ct);
        var since = DateTime.UtcNow.Date.AddMonths(-months);
        var me = email.ToLowerInvariant();

        var wroteTo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mine = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { me };
        var lastWrote = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);   // person -> newest time I wrote to them
        if (await FolderAsync(client, SpecialFolder.Sent, SentNames, ct) is { } sent)
        {
            await sent.OpenAsync(FolderAccess.ReadOnly, ct);
            var sentUids = await sent.SearchAsync(SearchQuery.DeliveredAfter(since.AddMonths(-12)), ct);
            foreach (var chunk in sentUids.Reverse().Take(5000).Chunk(500))
                foreach (var s in await sent.FetchAsync(chunk, MessageSummaryItems.Envelope | MessageSummaryItems.InternalDate, ct))
                {
                    foreach (var f in s.Envelope.From.Mailboxes) mine.Add(f.Address.ToLowerInvariant());
                    var at = s.Envelope.Date ?? s.InternalDate ?? DateTimeOffset.MinValue;
                    foreach (var a in s.Envelope.To.Mailboxes.Concat(s.Envelope.Cc.Mailboxes))
                    {
                        var addr = a.Address.ToLowerInvariant();
                        if (addr == me) continue;
                        wroteTo.Add(addr);
                        if (!lastWrote.TryGetValue(addr, out var was) || at > was) lastWrote[addr] = at;
                    }
                }
            await sent.CloseAsync(false, ct);
        }

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, ct);
        var uids = (await inbox.SearchAsync(SearchQuery.DeliveredAfter(since), ct)).Reverse().Take(maxMails).ToList();
        var request = new FetchRequest(MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.Flags | MessageSummaryItems.InternalDate)
        {
            Headers = new HeaderSet(GmailImap.ListHeaders),
        };
        var noTabs = new Dictionary<uint, GmailTab>();
        var mails = new List<MailHeader>(uids.Count);
        progress.Report((0, uids.Count));
        foreach (var chunk in uids.Chunk(500))
        {
            foreach (var s in await inbox.FetchAsync(chunk, request, ct))
            {
                var h = GmailImap.ToHeader(s, me, noTabs);
                mails.Add(lastWrote.TryGetValue(h.FromAddress.ToLowerInvariant(), out var wrote) && wrote > h.Date ? h with { RepliedLater = true } : h);
            }
            progress.Report((mails.Count, uids.Count));
        }
        await inbox.CloseAsync(false, ct);
        await client.DisconnectAsync(true, ct);
        wroteTo.ExceptWith(mine);
        return new MailboxScan(mails, wroteTo, mine);
    }

    public async Task<int> MoveToTrashAsync(IReadOnlyList<uint> uids, CancellationToken ct)
    {
        if (uids.Count == 0) return 0;
        if (uids.Count > CleanupRules.MaxPerAction) throw new InvalidOperationException("For mange mails på én gang.");
        using var client = await ConnectAsync(ct, 600_000);
        var trash = await FolderAsync(client, SpecialFolder.Trash, TrashNames, ct) ?? throw new InvalidOperationException("Outlook har ingen Slettet post-mappe.");
        await client.Inbox.OpenAsync(FolderAccess.ReadWrite, ct);
        var moved = 0;
        foreach (var chunk in uids.Select(u => new UniqueId(u)).Chunk(200))
        {
            var map = await client.Inbox.MoveToAsync(chunk, trash, ct);
            moved += map?.Source.Count ?? chunk.Length;
        }
        await client.DisconnectAsync(true, ct);
        return moved;
    }

    public Task<(int Total, int Trash, int Starred)> CountOldAsync(DateOnly before, OldMode mode, CancellationToken ct) =>
        throw new NotSupportedException("Ryd alt det gamle virker kun med Gmail endnu. Brug Ryd op ved afsenderne.");

    public Task<(int Trashed, int Archived)> ClearOldAsync(DateOnly before, OldMode mode, string label, CancellationToken ct) =>
        throw new NotSupportedException("Ryd alt det gamle virker kun med Gmail endnu.");

    public Task<int> RestoreLabelAsync(string label, CancellationToken ct) => Task.FromResult(0);

    public async Task<int> RestoreAsync(IReadOnlyList<string> messageIds, CancellationToken ct)
    {
        if (messageIds.Count == 0) return 0;
        using var client = await ConnectAsync(ct, 600_000);
        var trash = await FolderAsync(client, SpecialFolder.Trash, TrashNames, ct);
        if (trash is null) return 0;
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
}
