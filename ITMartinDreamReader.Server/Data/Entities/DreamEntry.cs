namespace ITMartinDreamReader.Server.Data.Entities;

public class DreamEntry
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
    public string Rating { get; set; } = "Medium"; // Bad, Medium, Nice
    public string? AiTitle { get; set; }
    public string? AiInterpretation { get; set; }
    public string? AiFunny { get; set; }
    public string? ImageFileName { get; set; }

    public List<DreamCategory> Categories { get; set; } = [];
}
