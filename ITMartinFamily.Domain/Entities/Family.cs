namespace ITMartinFamily.Domain.Entities;

public sealed class Family
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Password { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
