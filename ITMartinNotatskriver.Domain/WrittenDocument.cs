namespace ITMartinNotatskriver.Domain;

/// <summary>Rules about the text the AI sends back.</summary>
public static class WrittenDocument
{
    /// <summary>The AI answers only this word when the material turned out to hold CPR numbers or health data about a
    /// person (for example on a photo, which the text check can not read).</summary>
    public const string BlockedMarker = "BLOKERET";

    public const string TruncatedNote = "\n\n[Dokumentet blev for langt og er klippet af her]";

    public static bool IsBlocked(string answer) => answer.Trim().Trim('.') == BlockedMarker;

    /// <summary>A short line without a full stop, first or after a blank line, reads as a heading (bold in Word).</summary>
    public static bool IsHeading(IReadOnlyList<string> lines, int index)
    {
        var line = lines[index];
        return line.Length is > 0 and < 70 && !line.EndsWith('.') && !line.StartsWith("- ")
               && (index == 0 || lines[index - 1].Length == 0);
    }
}
