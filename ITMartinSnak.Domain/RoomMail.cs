using System.Text.RegularExpressions;

namespace ITMartinSnak.Domain;

// 2026-11-10 user: "when user signs up ... If i just open the customer and make a snak room and writes to him, will he receive the
// message?" - no, he did not know the room existed, and without "Slå beskeder til" nothing told him. Now:
// 1) the invitation: Kunder sends the room link by mail (on "Lav snak-rum", and once to every older customer);
// 2) the nudge: Martin wrote, 10 minutes went by, the customer has not read it and has no phone with beskeder in the room
//    -> ONE mail "Martin har skrevet til dig". The next one only after the customer has been in the room again (no nagging).
// Only the link goes in the mail - never what was written.
public static partial class RoomMailRules
{
    public static readonly TimeSpan NudgeAfter = TimeSpan.FromMinutes(10);

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")] private static partial Regex MailShape();

    /// <summary>"  Ole@Gmail.com " -> "ole@gmail.com"; "" when it is not a mail address.</summary>
    public static string CleanMail(string? mail)
    {
        var m = (mail ?? "").Trim().ToLowerInvariant();
        return m.Length <= 120 && MailShape().IsMatch(m) ? m : "";
    }

    /// <summary>Should the customer get a "Martin har skrevet" mail now?</summary>
    /// <param name="last">The newest message in the room.</param>
    /// <param name="seen">Read positions; the host's own row is skipped by name.</param>
    /// <param name="roomPhones">Phones with beskeder in this room (the host's phones are not in a room).</param>
    public static bool ShouldNudge(Room r, Message? last, IReadOnlyList<Seen> seen, int roomPhones, string hostName, DateTime nowUtc)
    {
        if (r.Mail.Length == 0 || r.IsDone || r.Gruppe || last is null || !last.Host || roomPhones > 0) return false;
        if (nowUtc - last.At < NudgeAfter) return false;
        var guests = seen.Where(s => !s.Name.Equals(hostName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (guests.Any(s => s.MessageId >= last.Id)) return false;                    // already read it
        if (r.MailedAt is not { } mailed) return true;
        // Mailed before: only again for a newer message AND after the customer has been in the room since that mail.
        return last.At > mailed && guests.Any(s => s.At > mailed);
    }

    private static string Hej(string? name)
    {
        var first = (name ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return first.Length == 0 ? "Hej" : "Hej " + first;
    }

    private const string Phone =
        "Få besked på telefonen, når jeg skriver: åbn linket på telefonen, tryk på de tre prikker (⋯) eller tre streger (☰) og vælg " +
        "\"Føj til hjemmeskærm\". Åbn Snak fra ikonet og tryk \"Slå beskeder til\".";

    public static (string Subject, string Body) Invitation(string? name, string link) =>
        ("Dit snak-rum hos ITMartin",
         $"{Hej(name)}\n\nJeg har lavet et lille rum, hvor vi kan skrive sammen:\n{link}\n\n" +
         "Tryk på linket og skriv dit navn, så er du inde. Skriv gerne der i stedet for på mail – så ser vi begge alt ét sted.\n\n" +
         Phone + "\n\nDer er ingen reklamer, og intet bliver delt. Når sagen er klaret, bliver rummet slettet.\n\nVenlig hilsen\nMartin\nITMartin.dk");

    public static (string Subject, string Body) Nudge(string? name, string link) =>
        ("Martin har skrevet til dig",
         $"{Hej(name)}\n\nJeg har skrevet til dig i dit snak-rum:\n{link}\n\n" +
         "Tryk på linket for at læse det og svare.\n\n" + Phone +
         " Så får du ikke flere mails om nye beskeder.\n\nVenlig hilsen\nMartin\nITMartin.dk");
}
