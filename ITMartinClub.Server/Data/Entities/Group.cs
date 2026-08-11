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

    public List<Member> Members { get; set; } = [];
    public List<Document> Documents { get; set; } = [];
    public List<CalendarEvent> Events { get; set; } = [];
    public List<Assignment> Assignments { get; set; } = [];
    public List<MainTask> MainTasks { get; set; } = [];
    public List<BulletinPost> Posts { get; set; } = [];
}
