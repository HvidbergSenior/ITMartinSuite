namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class ScreenshotReclassifyResult
{
    public int Checked { get; init; }
    public int KeptAsScreenshot { get; init; }
    public int MovedOut { get; init; }
    public int Failed { get; init; }
    public int RemainingOverCap { get; init; }
}
