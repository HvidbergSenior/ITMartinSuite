namespace ITMartinClub.Server.Data.Entities;

// "Beskeder med kvittering" (2026-10-02, Club as the free/paid product): something important for the team, and the sender
// can SEE who got it. One receipt per recipient; the personal Token is the mail link that also logs the member in.
public sealed class ClubMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid FromMemberId { get; set; }
    public string FromName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public bool RequireConfirm { get; set; } = true;   // "✅ Jeg har læst det" wanted, and reminders go out
    public string BaseUrl { get; set; } = "";          // the host it was sent from (hvidberg.itmartin.dk …) - links in mails/reminders use it
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<MessageReceipt> Receipts { get; set; } = [];
}

public sealed class MessageReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; }
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = "";
    public string Token { get; set; } = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    public DateTime? SeenAt { get; set; }        // opened the message (app or mail link)
    public DateTime? ConfirmedAt { get; set; }   // pressed "Jeg har læst det"
    public DateTime? MailedAt { get; set; }
    public DateTime? RemindedAt { get; set; }

    public ClubMessage Message { get; set; } = null!;
}
