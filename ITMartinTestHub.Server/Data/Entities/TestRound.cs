namespace ITMartinTestHub.Server.Data.Entities;

public sealed class TestRound
{
    public Guid     Id        { get; set; } = Guid.NewGuid();
    public string   Name      { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool     IsActive  { get; set; } = true;

    public List<TestAssignment> Assignments { get; set; } = [];
}
