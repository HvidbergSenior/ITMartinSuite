namespace ITMartinFamily.Domain.Entities;

public sealed class PersonalReminder
{
    public Guid      Id               { get; set; } = Guid.NewGuid();
    public Guid      FamilyId         { get; set; }
    public string    MemberName       { get; set; } = "";
    public string    Text             { get; set; } = "";
    public DateOnly  Date             { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public bool      Done             { get; set; }
    public DateTime  CreatedAt        { get; set; } = DateTime.UtcNow;
    public DateTime? RemindAt         { get; set; }
    public bool      NotificationSent { get; set; }
    public string?   PhotoPath        { get; set; }
}
