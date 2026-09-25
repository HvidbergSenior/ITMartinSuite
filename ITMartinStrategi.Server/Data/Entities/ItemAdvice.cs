namespace ITMartinStrategi.Server.Data.Entities;

/// <summary>Micromanagement advice for one thing you apply or build on a planet (an artifact,
/// an improvement …): what it is for, where to use it and where not. Title matches the
/// WikiEntry title, so the effect numbers come from the wiki and stay current; the advice
/// lives in its own table because the weekly wiki rebuild replaces every WikiEntry.</summary>
public sealed class ItemAdvice
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string Title { get; set; } = "";
    public string Group { get; set; } = "";     // "Planet artifacts", "Improvements" …
    public string Purpose { get; set; } = "";
    public string UseOn { get; set; } = "";
    public string AvoidOn { get; set; } = "";
    public string Example { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
