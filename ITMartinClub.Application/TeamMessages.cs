using ITMartinClub.Domain;

namespace ITMartinClub.Application;

public sealed record MessageList(List<ClubMessage> All, List<ClubMessage> Unconfirmed, int Others);

// Beskeder med kvittering: send = one receipt per member + push + mail with a personal link (/m/{token}).
// Opening = Set, the button = Bekræftet. One automatic reminder after 24 h (SendDueRemindersAsync), or on demand.
public sealed class TeamMessages(IMessageStore messages, ITeamStore teams, IPushNotifier push, IMailSender mail, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public bool MailConfigured => mail.Configured;

    public async Task<MessageList> ListAsync(Guid groupId, Guid memberId)
    {
        var all = await messages.ForMemberAsync(groupId, memberId, 50);
        var unconfirmed = all.Where(m => m.RequireConfirm && m.Receipts.Any(r => r.MemberId == memberId && r.ConfirmedAt == null)).ToList();
        var others = (await teams.MembersAsync(groupId)).Count(m => m.Id != memberId);
        return new MessageList(all, unconfirmed, others);
    }

    public async Task<ClubMessage> SendAsync(Group group, Member from, string title, string body, bool requireConfirm, string baseUrl)
    {
        var msg = new ClubMessage
        {
            GroupId = group.Id, FromMemberId = from.Id, FromName = from.Name,
            Title = title.Trim(), Body = body.Trim(), RequireConfirm = requireConfirm, BaseUrl = baseUrl.TrimEnd('/'),
        };
        var members = (await teams.MembersAsync(group.Id)).Where(m => m.Id != from.Id).ToList();
        foreach (var m in members) msg.Receipts.Add(new MessageReceipt { MessageId = msg.Id, MemberId = m.Id, MemberName = m.Name });
        await messages.AddAsync(msg);

        foreach (var r in msg.Receipts)
            await NotifyAsync(msg, r, members.First(m => m.Id == r.MemberId), reminder: false);
        await messages.UpdateReceiptsAsync(msg.Receipts);
        return msg;
    }

    // Push to the member's phones + mail if we have an address. Used for the first send and for reminders.
    private async Task NotifyAsync(ClubMessage msg, MessageReceipt r, Member m, bool reminder)
    {
        var link = $"{msg.BaseUrl}/m/{r.Token}";
        var title = reminder ? $"🔔 Påmindelse: {msg.Title}" : $"📣 {msg.Title}";
        await push.ToMembersAsync(msg.GroupId, [m.Name], title, $"Fra {msg.FromName}", link);

        if (EmailAddress.LooksLike(m.Email))
        {
            var text =
                $"Hej {m.Name}\n\n" +
                (reminder ? $"Du har ikke set denne besked fra {msg.FromName} endnu:\n\n" : $"{msg.FromName} har sendt en besked til jer:\n\n") +
                $"{msg.Title}\n{new string('-', Math.Min(msg.Title.Length, 60))}\n{msg.Body}\n\n" +
                (msg.RequireConfirm ? $"Åbn beskeden og tryk \"Jeg har læst det\":\n{link}\n" : $"Åbn beskeden:\n{link}\n") +
                "\nLinket er dit personlige – det logger dig ind i Club. Del det ikke med andre.\n" +
                // User rule: always say how to put the app on the phone/PC (kolibri.js shows the 📲 button on every page).
                "\nFå Club på telefonen eller PC'en: åbn linket og tryk på knappen \"📲 Læg på telefonen\" nederst på siden.\n" +
                "Så får du også beskederne som besked på telefonen.\n";
            if (await mail.SendAsync(m.Email!, msg.FromName, title, text))
            {
                if (reminder) r.RemindedAt = Now; else r.MailedAt = Now;
            }
        }
        if (reminder) r.RemindedAt ??= Now;   // push-only members count as reminded too - never twice automatically
    }

    /// <summary>The message (if it is the team's) and the member's own receipt - opening it in the app counts as seen.</summary>
    public async Task<(ClubMessage? Message, MessageReceipt? Mine)> OpenAsync(Guid groupId, Guid id, Guid memberId)
    {
        var msg = await messages.GetAsync(groupId, id);
        var mine = msg?.Receipts.FirstOrDefault(r => r.MemberId == memberId);
        if (mine is not null && mine.MarkSeen(Now)) await messages.UpdateReceiptsAsync([mine]);
        return (msg, mine);
    }

    /// <summary>The personal mail/push link /m/{token}: marks the message as seen. Null for an unknown token.</summary>
    public async Task<MessageReceipt?> OpenLinkAsync(string token)
    {
        var r = await messages.ReceiptByTokenAsync(token);
        if (r is not null && r.MarkSeen(Now)) await messages.UpdateReceiptsAsync([r]);
        return r;
    }

    public async Task ConfirmAsync(MessageReceipt r)
    {
        r.Confirm(Now);
        await messages.UpdateReceiptsAsync([r]);
    }

    // The sender's "🔔 Påmind dem, der mangler" - same as the automatic reminder, on demand.
    public async Task<int> RemindMissingAsync(ClubMessage msg)
    {
        var missing = msg.Missing();
        var members = await teams.MembersAsync(msg.GroupId);
        foreach (var r in missing)
            if (members.FirstOrDefault(m => m.Id == r.MemberId) is { } m)
            {
                await NotifyAsync(msg, r, m, reminder: true);
                r.RemindedAt = Now;
            }
        await messages.UpdateReceiptsAsync(missing);
        return missing.Count;
    }

    /// <summary>Messages that want confirmation, sent more than 24 h ago (and less than a week), get ONE reminder per missing member.</summary>
    public async Task<int> SendDueRemindersAsync()
    {
        var receipts = await messages.AwaitingReminderAsync(Now.AddDays(-7), Now - ClubMessage.RemindAfter);
        var membersByGroup = new Dictionary<Guid, List<Member>>();
        foreach (var r in receipts)
        {
            if (!membersByGroup.TryGetValue(r.Message.GroupId, out var members))
                membersByGroup[r.Message.GroupId] = members = await teams.MembersAsync(r.Message.GroupId);
            if (members.FirstOrDefault(m => m.Id == r.MemberId) is { } m)
                await NotifyAsync(r.Message, r, m, reminder: true);
        }
        if (receipts.Count > 0) await messages.UpdateReceiptsAsync(receipts);
        return receipts.Count;
    }

    /// <summary>The member's own mail for message copies. Returns the text to show.</summary>
    public async Task<string> SaveEmailAsync(Member member, string email)
    {
        var e = email.Trim();
        if (e.Length > 0 && !EmailAddress.LooksLike(e)) return "Det ligner ikke en mailadresse.";
        member.Email = e.Length == 0 ? null : e;
        await teams.UpdateMemberAsync(member);
        return e.Length == 0 ? "Mailen er fjernet – du får beskederne i appen og som push." : "✓ Gemt. Du får nye beskeder på mail.";
    }
}

// Board meeting (Skelagerhøjen, 2026-10-02): there is always a referent. Picking one creates the task "Skriv referat";
// sending the minutes posts them as a Besked med kvittering and closes that task.
public sealed class Meetings(ITeamItems<Meeting> meetings, ITeamItems<Assignment> tasks, TaskBoard board, TeamMessages messages, TimeProvider clock)
{
    private static readonly System.Globalization.CultureInfo Da = new("da-DK");

    public async Task<List<Meeting>> ListAsync(Guid groupId) =>
        (await meetings.ListAsync(groupId)).OrderByDescending(m => m.Date).ToList();

    public async Task AddAsync(Group group, Member by, DateTime date, string place, string referent, string agenda)
    {
        var meeting = new Meeting { GroupId = group.Id, Date = date, Place = place.Trim(), ReferentName = referent, Agenda = agenda.Trim() };
        var task = new Assignment
        {
            GroupId = group.Id, Title = $"Skriv referat fra mødet {meeting.Date.ToString("d. MMMM", Da)}", CreatedByName = by.Name,
            DueDate = meeting.Date.Date.AddDays(7), MainTaskId = await board.TopicIdAsync(group.Id, "Møder"),
        };
        task.AddAssignee(referent);
        meeting.ReferatTaskId = task.Id;
        await tasks.AddAsync(task);
        await meetings.AddAsync(meeting);
    }

    public Task SaveAsync(Meeting meeting) => meetings.UpdateAsync(meeting);

    /// <summary>Sends the minutes as a message with receipts, closes the referat task; returns the message id.</summary>
    public async Task<Guid> SendReferatAsync(Group group, Member by, Meeting m, string baseUrl)
    {
        var msg = await messages.SendAsync(group, by, $"Referat fra mødet {m.Date.ToString("d. MMMM", Da)}", m.Referat, requireConfirm: true, baseUrl);
        m.SentMessageId = msg.Id;
        await meetings.UpdateAsync(m);
        if (m.ReferatTaskId is { } tid && await tasks.GetAsync(group.Id, tid) is { IsCompleted: false } task)
        {
            task.Complete(by.Name, clock.GetUtcNow().UtcDateTime);
            await tasks.UpdateAsync(task);
        }
        return msg.Id;
    }
}
