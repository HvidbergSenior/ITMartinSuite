namespace ITMartin.Ai.Models;

public sealed class PhotoOrientationResult
{
    public string RelativePath { get; set; } = string.Empty;
    public bool NeedsRotation { get; set; }
    public int DegreesNeeded { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}
