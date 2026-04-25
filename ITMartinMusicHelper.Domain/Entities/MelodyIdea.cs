namespace ITMartinMusicHelper.Domain.Entities;

public class MelodyIdea
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Id { get; set; } = default!;
    // Optional: tie to specific chord
    public string? TargetChord { get; set; }
}