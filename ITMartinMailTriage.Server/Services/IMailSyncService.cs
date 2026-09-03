using ITMartinMailTriage.Server.Data;

namespace ITMartinMailTriage.Server.Services;

public sealed record FetchedEmail(
    string MessageId,
    string From,
    string Subject,
    string Snippet,
    DateTimeOffset ReceivedAtUtc);

public interface IMailSyncService
{
    MailAccount Account { get; }

    /// <summary>
    /// Fetches up to maxCount of the most recent inbox messages. Callers
    /// dedupe against already-synced MessageIds - this always returns from
    /// the top of the inbox, it does not track a "since last sync" cursor
    /// itself.
    /// </summary>
    Task<List<FetchedEmail>> FetchRecentAsync(int maxCount, CancellationToken cancellationToken = default);
}
