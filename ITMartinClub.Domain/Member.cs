namespace ITMartinClub.Domain;

public sealed class Member
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;
    public string? ContactInfo { get; set; }
    public string? Email { get; set; }   // messages are also mailed here (with a personal log-in link)
    public string? LoginToken { get; set; }   // invitation link /l/{token} - logs in without PIN; new token = old link stops working

    // Free-text, not an enum - vocabulary differs per group (Leder/Medlem for a
    // club, Ejer/Medarbejder for a shop, Selv/Støtte for the ADHD use case).
    // Purely a label for now: shown on Medlemmer, set by admin - no permission
    // gating tied to it yet.
    public string? Role { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Group Group { get; set; } = null!;
    public List<MemberSession> Sessions { get; set; } = [];
    public List<BulletinPost> Posts { get; set; } = [];

    /// <summary>Admin = a role containing "Admin" (set on the Admin page). Only they invite and manage people.</summary>
    public bool IsAdmin => Role?.Contains("Admin", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>A new invitation link (/l/{token}); the old one stops working.</summary>
    public void NewLoginToken() => LoginToken = Tokens.New();

    /// <summary>ContactInfo is free text ("ITMartin@Mensa.dk, 31194730"): the first Danish-looking number in it.</summary>
    public string? Phone
    {
        get
        {
            var x = System.Text.RegularExpressions.Regex.Match(ContactInfo ?? "", @"(\+?\d[\d ]{6,14}\d)");
            return x.Success ? x.Value.Replace(" ", "") : null;
        }
    }
}
