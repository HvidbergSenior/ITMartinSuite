namespace ITMartinHjem.Domain;

/// <summary>Texts for "Opret dig som interesseret" on the service pages (user 2026-11-23): sign-up + a chat room in one go.</summary>
public static class InterestedChatRules
{
    /// <summary>The first message in the room: which help they came from, then their own words (or a plain default).</summary>
    public static string FirstMessage(string service, string? message)
    {
        var s = (service ?? "").Trim();
        var m = (message ?? "").Trim();
        if (m.Length == 0) m = "Jeg vil gerne høre mere.";
        var text = s.Length == 0 ? m : $"{s}: {m}";
        return text.Length > SnakStartRules.MaxText ? text[..SnakStartRules.MaxText] : text;
    }

    /// <summary>What goes into Kunder as their wish (kept short - TrialRules cuts it at MaxNote).</summary>
    public static string Note(string service, string? message)
    {
        var s = (service ?? "").Trim();
        var m = (message ?? "").Trim();
        return m.Length == 0 ? s : s.Length == 0 ? m : $"{s}: {m}";
    }

    /// <summary>Signed up fine, but Snak said no: they still have their number, and Martin got the mail.</summary>
    public static string SignedUpButNoRoom(string code, string? why) =>
        (code.Length > 0 ? $"Du er skrevet op – dit nummer er {code}. " : "Du er skrevet op. ") +
        "Chatten kunne ikke startes lige nu, men jeg har fået besked og skriver til dig på mail." +
        (string.IsNullOrWhiteSpace(why) ? "" : $" ({why.Trim()})");
}
