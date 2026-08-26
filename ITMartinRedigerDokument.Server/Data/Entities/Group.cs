namespace ITMartinRedigerDokument.Server.Data.Entities;

public sealed class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // Plaintext by design - a room code shown to the admin and shared with
    // teammates who should join, not a real secret. AdminPin gates
    // destructive actions and is hashed like one. Same convention as Club.
    public string InviteCode { get; set; } = string.Empty;
    public string AdminPin { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Member> Members { get; set; } = [];
    public List<Document> Documents { get; set; } = [];
    public List<MainTask> MainTasks { get; set; } = [];
    public List<Assignment> Assignments { get; set; } = [];
}
