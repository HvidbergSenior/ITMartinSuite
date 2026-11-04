namespace ITMartinNotatskriver.Domain;

/// <summary>Rules about the text the AI sends back.</summary>
public static class WrittenDocument
{
    /// <summary>The AI answers only this word when the material turned out to hold CPR numbers or health data about a
    /// person (for example on a photo, which the text check can not read).</summary>
    public const string BlockedMarker = "BLOKERET";

    public const string TruncatedNote = "\n\n[Dokumentet blev for langt og er klippet af her]";

    // "BLOKERET" alone, or "BLOKERET: <the line it stopped at>" (2026-11-04: the user wants to know WHAT to remove).
    public static bool IsBlocked(string answer)
    {
        var first = FirstLine(answer);
        return first == BlockedMarker || first.StartsWith(BlockedMarker + ":", StringComparison.Ordinal);
    }

    /// <summary>What the AI stopped at - the user's own line (or what is on the photo), shown only to the user. "" if none.</summary>
    public static string BlockReason(string answer)
    {
        if (!IsBlocked(answer)) return "";
        var reason = answer.Trim()[BlockedMarker.Length..].TrimStart(':', ' ', '.');
        reason = string.Join(' ', reason.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim().Trim('"', '«', '»').Trim();
        return reason.Length > 200 ? reason[..200] + "…" : reason;
    }

    private static string FirstLine(string answer)
    {
        var t = answer.Trim();
        var nl = t.IndexOf('\n');
        return (nl < 0 ? t : t[..nl]).Trim().TrimEnd('.');
    }

    /// <summary>A short line without a full stop, first or after a blank line, reads as a heading (bold in Word).</summary>
    public static bool IsHeading(IReadOnlyList<string> lines, int index)
    {
        var line = lines[index];
        return line.Length is > 0 and < 70 && !line.EndsWith('.') && !line.StartsWith("- ")
               && (index == 0 || lines[index - 1].Length == 0);
    }
}
