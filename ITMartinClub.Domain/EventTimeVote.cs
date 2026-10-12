namespace ITMartinClub.Server.Data.Entities;

public sealed class EventTimeVote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SuggestionId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string Status { get; set; } = "Yes"; // Yes, No
}
