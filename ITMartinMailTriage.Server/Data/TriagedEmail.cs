namespace ITMartinMailTriage.Server.Data;

public enum MailAccount
{
    Gmail,
    Outlook
}

/// <summary>
/// One synced-and-scored email. Primary key is (Account, MessageId) so the
/// same message id from two different providers never collides.
/// </summary>
public sealed class TriagedEmail
{
    public string Id { get; set; } = ""; // $"{Account}:{MessageId}"
    public MailAccount Account { get; set; }
    public string MessageId { get; set; } = "";
    public string From { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Snippet { get; set; } = "";
    public DateTimeOffset ReceivedAtUtc { get; set; }

    // Null until a Claude batch has scored it - lets sync and scoring run as
    // separate, independently-resumable passes.
    public bool? NeedsResponse { get; set; }
    public int? RelevanceScore { get; set; }
    public string? Reasoning { get; set; }
    public DateTimeOffset? ScoredAtUtc { get; set; }

    public bool Dismissed { get; set; }
}
