namespace ITMartinTransit.Server.Data;

public class TransitPerson
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string PhotoBase64 { get; set; } = "";
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}
