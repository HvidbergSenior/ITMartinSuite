namespace ITMartinTestHub.Server.Data.Entities;

public sealed class Feedback
{
    public Guid         Id               { get; set; } = Guid.NewGuid();
    public Guid         TestAssignmentId { get; set; }
    public Guid         AppEntryId       { get; set; }
    public Guid         TesterId         { get; set; }
    public string       Text             { get; set; } = "";
    public FeedbackType Type             { get; set; } = FeedbackType.Idea;
    public DateTime     CreatedAt        { get; set; } = DateTime.UtcNow;

    public Tester? Tester { get; set; }
}

public enum FeedbackType { Bug, Idea, Comment }
