using ITMartinClub.Server.Data;
using ITMartinClub.Server.Data.Entities;

namespace ITMartinClub.Server.Services;

// "📧 Inviter" on Medlemmer: a mail with a personal link (/l/{token}) that logs the member straight in - no PIN, no code.
// Sending again makes a NEW link (the old one stops working), so a lost or forwarded mail can be fixed by re-inviting.
public sealed class InviteService(ClubDbContext db, ClubMailService mail)
{
    public async Task<bool> SendAsync(Group group, Member member, Member from, string baseUrl)
    {
        if (!ClubMailService.LooksLikeEmail(member.Email)) return false;
        member.LoginToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        await db.SaveChangesAsync();
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
}
