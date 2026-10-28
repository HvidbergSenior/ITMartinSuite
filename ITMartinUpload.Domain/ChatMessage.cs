namespace ITMartinUpload.Domain;

public sealed record ChatMessage(string From, string Text, DateTime AtUtc)
{
    public const string Customer = "kunde";
    public const string Martin = "martin";

    public bool IsFromMartin => From == Martin;
}

/// <summary>The rules for the conversation with one customer: what is worth storing and what is still unanswered.</summary>
public static class Conversation
{
    public const int MaxTextLength = 2000;

    /// <summary>The text as stored (trimmed, at most 2000 characters), or null when there is nothing worth storing.</summary>
    public static string? Clean(string? text)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0) return null;
        return text.Length > MaxTextLength ? text[..MaxTextLength] : text;
    }

    /// <summary>The first 80 characters, for a push notification.</summary>
    public static string Preview(string text) => text.Length > 80 ? text[..80] + "…" : text;

    /// <summary>Messages from the customer that Martin has not answered yet.</summary>
    public static int UnansweredCount(IReadOnlyList<ChatMessage> thread)
    {
        for (var i = thread.Count - 1; i >= 0; i--)
            if (thread[i].IsFromMartin) return thread.Count - (i + 1);
        return thread.Count;
    }
}
