namespace ITMartinClub.Server.Data.Entities;

public sealed class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public string AdminPin { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // "Gaming" (default, existing groups) keeps every ready-check/match/bulletin
    // feature; "Practical" hides all of that for groups doing real-world project
    // coordination instead of a hobby club - see GroupHome.razor's Kind checks.
    public string Kind { get; set; } = "Gaming";

    // Optional "must be done by" deadline, shown as a countdown in Admin's
    // Fremdrift section - generic, not specific to any one group's project.
    public DateTime? TargetDate { get; set; }

    public List<Member> Members { get; set; } = [];
    public List<Document> Documents { get; set; } = [];
    public List<CalendarEvent> Events { get; set; } = [];
    public List<Assignment> Assignments { get; set; } = [];
    public List<BulletinPost> Posts { get; set; } = [];
}
