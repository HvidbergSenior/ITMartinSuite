namespace ITMartinFamily.Domain.Entities;

public sealed class PushSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FamilyId { get; set; }
    public string MemberName { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string P256DH { get; set; } = "";
    public string Auth { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
