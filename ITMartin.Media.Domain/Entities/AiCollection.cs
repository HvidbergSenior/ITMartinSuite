namespace ITMartin.Media.Domain.Entities;

public class AiCollection
{
    public string Title { get; set; } = "";

    public string Description { get; set; } = "";

    public string Category { get; set; } = "";

    public string? CoverImage { get; set; }

    public List<MediaFile> Files { get; set; } = [];

    public int Year { get; set; }

    public int Month { get; set; }
}