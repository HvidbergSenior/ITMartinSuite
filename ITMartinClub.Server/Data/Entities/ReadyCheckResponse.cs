namespace ITMartinClub.Server.Data.Entities;

public sealed class ReadyCheckResponse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReadyCheckId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string Status { get; set; } = "Ready"; // Ready, NotNow, Watching (spectating - not playing but wants live highlights)
    public DateTime RespondedAt { get; set; } = DateTime.UtcNow;
}
