namespace ITMartinRewlhul.Server.Data;

public sealed class PlayerState
{
    public string Name { get; set; } = string.Empty;
    public int Progress { get; set; } // correct taps so far in the current level's attempt
}
