using ITMartinSnak.Domain;

namespace ITMartinSnak.Application;

// 2026-11-10: mail to customers - the invitation (from Kunder) and the "Martin har skrevet" nudge (every minute). Rules in RoomMailRules.
public sealed class RoomMails(Rooms rooms, IRoomMailer mailer, IPushSender push, TimeProvider clock, string hostName)
{
    /// <summary>Kunder: "send the link to this customer". Saves the address on the room and mails the link.</summary>
    /// <returns>Null when sent, else why not (Danish, for Martin).</returns>
    public async Task<string?> InviteAsync(string? link, string? mail, string? name, CancellationToken ct = default)
    {
        var key = (link ?? "").Trim().TrimEnd('/');
        var cut = key.LastIndexOf("/r/", StringComparison.Ordinal);
        if (cut >= 0) key = key[(cut + 3)..];
        if (rooms.ByKey(key) is not { } r) return "Snak kender ikke det rum.";
        var to = RoomMailRules.CleanMail(mail);
        if (to.Length == 0) return "Kunden har ikke en rigtig mailadresse.";
        rooms.SetMail(r.Id, to);
        var (subject, body) = RoomMailRules.Invitation(name, rooms.Link(r));
        return await mailer.SendAsync(to, subject, body, ct) ? null : "Mailen kom ikke af sted. Prøv igen om lidt.";
    }

    /// <summary>Every minute: one mail to customers who have not read Martin's newest message and get no beskeder.</summary>
    public async Task<int> NudgeAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var sent = 0;
        foreach (var r in rooms.Open())
        {
            if (r.Mail.Length == 0 || r.Gruppe) continue;
            var g = rooms.GroupOf(r);
            if (!RoomMailRules.ShouldNudge(r, g.Last, g.SeenList(), push.PhonesPerName(r.Id).Values.Sum(), hostName, now)) continue;
            var guest = g.SeenList().FirstOrDefault(s => !s.Name.Equals(hostName, StringComparison.OrdinalIgnoreCase))?.Name;
            var (subject, body) = RoomMailRules.Nudge(guest, rooms.Link(r));
            // Marked even when the mail fails, so a broken mail server never means a mail every minute.
            rooms.MarkMailed(r.Id);
            if (await mailer.SendAsync(r.Mail, subject, body, ct)) sent++;
        }
        return sent;
    }
}
