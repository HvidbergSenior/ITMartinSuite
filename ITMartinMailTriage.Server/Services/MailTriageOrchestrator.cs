using ITMartin.Ai.Interfaces;
using ITMartinMailTriage.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ITMartinMailTriage.Server.Services;

public sealed record TriageRunResult(int NewEmailsSynced, int EmailsScored, int ClaudeCallsUsed, bool HitCap);

/// <summary>
/// Ties sync + scoring together following CLAUDE.md's AI cost-discipline
/// rules for this suite: emails are batched into one Claude call each
/// (BatchSize per call, never one call per email), a hard MaxCallsPerRun
/// caps total spend regardless of how large the inbox is, and only
/// never-scored messages are sent - a second run only costs what's new.
/// </summary>
public sealed class MailTriageOrchestrator(
    IEnumerable<IMailSyncService> syncServices,
    IEmailRelevanceService relevanceService,
    MailTriageDbContext db,
    ILogger<MailTriageOrchestrator> logger)
{
    private const int BatchSize = 20;
    private const int MaxCallsPerRun = 10; // hard cap: at most 200 emails scored per run

    public async Task<TriageRunResult> RunAsync(int fetchPerAccount = 50, CancellationToken cancellationToken = default)
    {
        var newCount = await SyncAsync(fetchPerAccount, cancellationToken);
        var (scored, calls, hitCap) = await ScoreUnscoredAsync(cancellationToken);
        return new TriageRunResult(newCount, scored, calls, hitCap);
    }

    private async Task<int> SyncAsync(int fetchPerAccount, CancellationToken cancellationToken)
    {
        var newCount = 0;

        foreach (var sync in syncServices)
        {
            List<FetchedEmail> fetched;
            try
            {
                fetched = await sync.FetchRecentAsync(fetchPerAccount, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sync failed for {Account}", sync.Account);
                continue;
            }

            foreach (var email in fetched)
            {
                var id = $"{sync.Account}:{email.MessageId}";
                var exists = await db.Emails.AnyAsync(e => e.Id == id, cancellationToken);
                if (exists) continue;

                db.Emails.Add(new TriagedEmail
                {
                    Id = id,
                    Account = sync.Account,
                    MessageId = email.MessageId,
                    From = email.From,
                    Subject = email.Subject,
                    Snippet = email.Snippet,
                    ReceivedAtUtc = email.ReceivedAtUtc
                });
                newCount++;
            }
        }

        if (newCount > 0)
            await db.SaveChangesAsync(cancellationToken);

        return newCount;
    }

    private async Task<(int scored, int calls, bool hitCap)> ScoreUnscoredAsync(CancellationToken cancellationToken)
    {
        var profile = await db.Profile.FirstOrDefaultAsync(cancellationToken);
        var profileText = profile?.UserProfileText ?? "";

        var unscored = await db.Emails
            .Where(e => e.ScoredAtUtc == null)
            .OrderByDescending(e => e.ReceivedAtUtc)
            .Take(BatchSize * MaxCallsPerRun)
            .ToListAsync(cancellationToken);

        var scoredCount = 0;
        var calls = 0;

        foreach (var chunk in unscored.Chunk(BatchSize))
        {
            if (calls >= MaxCallsPerRun) break;

            var emailSummaries = chunk.Select(e => new EmailSummary(
                e.Id, e.From, e.Subject, e.Snippet, e.ReceivedAtUtc)).ToList();

            List<EmailRelevanceResult> results;
            try
            {
                results = await relevanceService.ScoreBatchAsync(emailSummaries, profileText, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Relevance scoring failed for a batch of {Count} emails", chunk.Length);
                calls++;
                continue;
            }

            calls++;

            var byId = chunk.ToDictionary(e => e.Id);
            foreach (var r in results)
            {
                if (!byId.TryGetValue(r.MessageId, out var email)) continue;

                email.NeedsResponse = r.NeedsResponse;
                email.RelevanceScore = r.RelevanceScore;
                email.Reasoning = r.Reasoning;
                email.ScoredAtUtc = DateTimeOffset.UtcNow;
                scoredCount++;
            }
        }

        if (scoredCount > 0)
            await db.SaveChangesAsync(cancellationToken);

        var hitCap = calls >= MaxCallsPerRun;
        if (hitCap)
            logger.LogWarning("Hit MaxCallsPerRun ({Cap}) - some emails remain unscored, run again to continue", MaxCallsPerRun);

        return (scoredCount, calls, hitCap);
    }
}
