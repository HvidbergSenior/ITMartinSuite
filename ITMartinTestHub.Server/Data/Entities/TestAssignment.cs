namespace ITMartinTestHub.Server.Data.Entities;

public sealed class TestAssignment
{
    public Guid      Id          { get; set; } = Guid.NewGuid();
    public Guid      TestRoundId { get; set; }
    public Guid      AppEntryId  { get; set; }
    public Guid      TesterId    { get; set; }
    public DateTime  CreatedAt   { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt   { get; set; }
    public DateTime? CompletedAt { get; set; }

    public AppEntry?  App     { get; set; }
    public Tester?    Tester  { get; set; }
    public TestRound? Round   { get; set; }

    public string? Purpose { get; set; }

    public List<StepResult> Results   { get; set; } = [];
    public List<Feedback>   Feedbacks { get; set; } = [];
}
