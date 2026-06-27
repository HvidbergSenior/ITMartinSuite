namespace ITMartinSuite.Maui.Models;

public class AppEntry
{
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";
    public string Description { get; init; } = "";
    public string Tags { get; init; } = "";
    public string GradientStart { get; init; } = "#1a1a2e";
    public string GradientEnd { get; init; } = "#16213e";
    public string? WebUrl { get; init; }
    public string? MauiRoute { get; init; }
}

public class AppCategory
{
    public string Name { get; init; } = "";
    public List<AppEntry> Apps { get; init; } = [];
}
