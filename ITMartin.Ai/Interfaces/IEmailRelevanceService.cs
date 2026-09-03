namespace ITMartin.Ai.Interfaces;

public sealed record EmailSummary(
    string MessageId,
    string From,
    string Subject,
    string Snippet,
    DateTimeOffset ReceivedAt);

public sealed record EmailRelevanceResult(
    string MessageId,
    bool NeedsResponse,
    int RelevanceScore,
    string Reasoning);

/// <summary>
/// Scores a batch of emails for personal relevance in one Claude call - never
/// one call per email (see CLAUDE.md's AI cost-discipline rules). Callers are
/// responsible for the incremental-skip and hard-cap-per-run behavior that
/// rule also requires.
/// </summary>
public interface IEmailRelevanceService
{
    Task<List<EmailRelevanceResult>> ScoreBatchAsync(
        IReadOnlyList<EmailSummary> emails,
        string userProfile,
        CancellationToken cancellationToken = default);
}
