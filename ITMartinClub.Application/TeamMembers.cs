using ITMartinClub.Domain;

namespace ITMartinClub.Application;

// Medlemmer, Adresser (Folk + invitations) and the Admin page's member list.
public sealed class TeamMembers(ITeamStore teams, IMailSender mail)
{
    public bool MailConfigured => mail.Configured;

    public Task<List<Member>> ListAsync(Guid groupId) => teams.MembersAsync(groupId);

    // "📧 Inviter" on Medlemmer: a mail with a personal link (/l/{token}) that logs the member straight in - no PIN, no code.
    // Sending again makes a NEW link (the old one stops working), so a lost or forwarded mail can be fixed by re-inviting.
    public async Task<bool> InviteAsync(Group group, Member member, Member from, string baseUrl)
    {
        if (!EmailAddress.LooksLike(member.Email)) return false;
        member.NewLoginToken();
        await teams.UpdateMemberAsync(member);
        var link = $"{baseUrl.TrimEnd('/')}/l/{member.LoginToken}";
        var text =
            $"Hej {member.Name}\n\n" +
            $"{from.Name} har inviteret dig til {group.Name} i Club.\n\n" +
            "Her kommer vigtige beskeder fra bestyrelsen – og du trykker \"✅ Jeg har læst det\", så vi ved, at alle har fået dem.\n\n" +
            $"Tryk på linket for at komme ind (ingen kode):\n{link}\n\n" +
            "Få Club på telefonen eller PC'en: åbn linket og tryk på knappen \"📲 Læg på telefonen\" nederst på siden.\n" +
            "Tryk også på \"🔔 Slå notifikationer til\", så får du beskederne med det samme.\n\n" +
            "Linket er dit personlige – del det ikke med andre.\n";
        return await mail.SendAsync(member.Email!, from.Name, $"Invitation: {group.Name} i Club", text);
    }

    /// <summary>A new invitation link for the member (Adresser: "🔗 Lav link" / "↻ Nyt link").</summary>
    public async Task MakeLinkAsync(Member member)
    {
        member.NewLoginToken();
        await teams.UpdateMemberAsync(member);
    }

    /// <summary>A person added here logs in only through their link (no PIN), like the board version. Returns an error text or null.</summary>
    public async Task<string?> AddPersonAsync(Guid groupId, string name, string phone)
    {
        name = name.Trim();
        if (name.Length == 0) return "Skriv et navn.";
        if ((await teams.MembersAsync(groupId)).Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return $"{name} er her allerede.";
        var m = new Member { GroupId = groupId, Name = name, Pin = "", ContactInfo = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim() };
        m.NewLoginToken();
        await teams.AddMemberAsync(m);
        return null;
    }

    /// <summary>Sessions and posts go with the member; tasks keep the name as text.</summary>
    public Task RemoveAsync(Guid groupId, Guid memberId) => teams.RemoveMemberAsync(groupId, memberId);

    /// <summary>The member picks a new PIN at the next join. Returns the member's name, or null.</summary>
    public async Task<string?> ResetPinAsync(Guid groupId, Guid memberId)
    {
        var m = await teams.MemberAsync(groupId, memberId);
        if (m is null) return null;
        m.Pin = "";
        await teams.UpdateMemberAsync(m);
        return m.Name;
    }

    public async Task SetRoleAsync(Guid groupId, Guid memberId, string role)
    {
        var m = await teams.MemberAsync(groupId, memberId);
        if (m is null) return;
        m.Role = string.IsNullOrWhiteSpace(role) ? null : role.Trim();
        await teams.UpdateMemberAsync(m);
    }

    /// <summary>"Mit nummer" on Adresser: mail and phone into ContactInfo; the mail also becomes the message mail.</summary>
    public async Task SaveContactAsync(Member member, string phone, string email)
    {
        phone = phone.Trim(); email = email.Trim();
        member.ContactInfo = string.Join(", ", new[] { email, phone }.Where(s => s.Length > 0));
        member.Email = email.Length > 0 ? email : member.Email;
        await teams.UpdateMemberAsync(member);
    }
}
