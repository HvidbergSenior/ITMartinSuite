namespace ITMartinR6Assistant.Domain.Entities;

public class SiteRecommendation
{
    public Guid Id { get; init; }

    public string MapName { get; init; } = string.Empty;

    public string SiteName { get; init; } = string.Empty;

    public List<string> Attackers { get; init; } = [];

    public List<string> Defenders { get; init; } = [];

    public List<string> Bans { get; init; } = [];

    public string Notes { get; init; } = string.Empty;
}