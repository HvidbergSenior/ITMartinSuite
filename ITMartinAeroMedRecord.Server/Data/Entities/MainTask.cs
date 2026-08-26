namespace ITMartinAeroMedRecord.Server.Data.Entities;

// A task column - every group starts with one ("Tasks") and adds more as
// needed. Same shape as Club's MainTask/Assignment board.
public sealed class MainTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Group Group { get; set; } = null!;
    public List<Assignment> Assignments { get; set; } = [];
}
