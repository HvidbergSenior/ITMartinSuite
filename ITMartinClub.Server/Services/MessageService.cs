using ITMartinClub.Server.Data;
using ITMartinClub.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinClub.Server.Services;

// Beskeder med kvittering: send = one receipt per member + push + mail with a personal link (/m/{token}).
// Opening = Set, the button = Bekræftet. Reminders: MessageReminderService.
public sealed class MessageService(ClubDbContext db, ClubPushService push, ClubMailService mail)
{
    public static readonly TimeSpan RemindAfter = TimeSpan.FromHours(24);

    public async Task<ClubMessage> SendAsync(Group group, Member from, string title, string body, bool requireConfirm, string baseUrl)
    {
        var msg = new ClubMessage
        {
            GroupId = group.Id, FromMemberId = from.Id, FromName = from.Name,
            Title = title.Trim(), Body = body.Trim(), RequireConfirm = requireConfirm, BaseUrl = baseUrl.TrimEnd('/'),
        };
        var members = await db.Members.Where(m => m.GroupId == group.Id && m.Id != from.Id).ToListAsync();
        foreach (var m in members) msg.Receipts.Add(new MessageReceipt { MemberId = m.Id, MemberName = m.Name });
        db.Messages.Add(msg);
        await db.SaveChangesAsync();

        foreach (var r in msg.Receipts)
            await NotifyAsync(msg, r, members.First(m => m.Id == r.MemberId), reminder: false);
        await db.SaveChangesAsync();
        return msg;
    }

    // Push to the member's phones + mail if we have an address. Used for the first send and for reminders.
    public async Task NotifyAsync(ClubMessage msg, MessageReceipt r, Member m, bool reminder)
    {
        var link = $"{msg.BaseUrl}/m/{r.Token}";
        var title = reminder ? $"🔔 Påmindelse: {msg.Title}" : $"📣 {msg.Title}";
        await push.SendToMembersAsync(db, msg.GroupId, [m.Name], title, $"Fra {msg.FromName}", link);

        if (ClubMailService.LooksLikeEmail(m.Email))
        {
            var text =
                $"Hej {m.Name}\n\n" +
                (reminder ? $"Du har ikke set denne besked fra {msg.FromName} endnu:\n\n" : $"{msg.FromName} har sendt en besked til jer:\n\n") +
                $"{msg.Title}\n{new string('-', Math.Min(msg.Title.Length, 60))}\n{msg.Body}\n\n" +
                (msg.RequireConfirm ? $"Åbn beskeden og tryk \"Jeg har læst det\":\n{link}\n" : $"Åbn beskeden:\n{link}\n") +
                "\nLinket er dit personlige – det logger dig ind i Club. Del det ikke med andre.\n";
            if (await mail.SendAsync(m.Email!, msg.FromName, title, text))
            {
                if (reminder) r.RemindedAt = DateTime.UtcNow; else r.MailedAt = DateTime.UtcNow;
            }
        }
        if (reminder) r.RemindedAt ??= DateTime.UtcNow;   // push-only members count as reminded too - never twice automatically
    }

    public async Task MarkSeenAsync(MessageReceipt r)
    {
        if (r.SeenAt is not null) return;
        r.SeenAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task ConfirmAsync(MessageReceipt r)
    {
        r.SeenAt ??= DateTime.UtcNow;
        r.ConfirmedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    // The sender's "🔔 Påmind dem, der mangler" - same as the automatic reminder, on demand.
    public async Task<int> RemindMissingAsync(ClubMessage msg)
    {
        var missing = msg.Receipts.Where(r => msg.RequireConfirm ? r.ConfirmedAt is null : r.SeenAt is null).ToList();
        var ids = missing.Select(r => r.MemberId).ToList();
        var members = await db.Members.Where(m => ids.Contains(m.Id)).ToListAsync();
        foreach (var r in missing)
            if (members.FirstOrDefault(m => m.Id == r.MemberId) is { } m)
            {
                await NotifyAsync(msg, r, m, reminder: true);
                r.RemindedAt = DateTime.UtcNow;
            }
        await db.SaveChangesAsync();
        return missing.Count;
    }
}

// Every 15 min: messages that want confirmation, sent more than 24 h ago, get ONE automatic reminder per missing member.
public sealed class MessageReminderService(IServiceScopeFactory scopes, ILogger<MessageReminderService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ClubDbContext>();
                var svc = scope.ServiceProvider.GetRequiredService<MessageService>();
                var due = DateTime.UtcNow - MessageService.RemindAfter;
                var weekAgo = DateTime.UtcNow.AddDays(-7);   // never dig up old messages
                var receipts = await db.Receipts.Include(r => r.Message)
                    .Where(r => r.Message.RequireConfirm && r.ConfirmedAt == null && r.RemindedAt == null
                                && r.Message.CreatedAt < due && r.Message.CreatedAt > weekAgo)
                    .ToListAsync(ct);
                foreach (var r in receipts)
                    if (await db.Members.FindAsync([r.MemberId], ct) is { } m)
                        await svc.NotifyAsync(r.Message, r, m, reminder: true);
                if (receipts.Count > 0) { await db.SaveChangesAsync(ct); log.LogInformation("Sent {N} message reminders", receipts.Count); }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested) { log.LogWarning(ex, "Message reminder pass failed"); }
            await Task.Delay(TimeSpan.FromMinutes(15), ct);
        }
    }
}
