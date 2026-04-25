namespace ITMartinSongCreator.Domain.Entities;

public class PracticeStep
{
    public string Section { get; set; } = string.Empty;
    public List<string> Chords { get; set; } = new();
    public List<string> BassStrings { get; set; } = new();  // one bass per chord
    public string PickingPattern { get; set; } = string.Empty;
    public List<string> MelodyTips { get; set; } = new();
    public string? Note { get; set; }
}