namespace ITMartinMusicHelper.Domain.Entities;

public class ChordProgression
{
    public string Name { get; set; } = string.Empty;
    public string Mood { get; set; } = string.Empty;
    public List<string> Chords { get; set; } = new();

    // Map each chord to a bass string (optional, default can be "E")
    public Dictionary<string, string>? BassMapping { get; set; }
}