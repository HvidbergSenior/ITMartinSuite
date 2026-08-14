namespace ITMartinLibrary.Domain.Entities;

public class LibraryGroup
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
