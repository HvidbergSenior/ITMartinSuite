using ITMartinPoll.Domain;

namespace ITMartinPoll.Application;

/// <summary>Takes the personal data out of every poll PrivacyRules says is old enough. Implemented in Infrastructure.</summary>
public interface IPersonalDataCleaner
{
    /// <returns>How many polls (of any kind) were cleaned now.</returns>
    Task<int> ForgetOldAsync(DateTime nowUtc, CancellationToken ct = default);
}

// The nightly GDPR clean-up (user 2026-10-24).
public sealed class Privacy(IPersonalDataCleaner cleaner, TimeProvider clock)
{
    public Task<int> CleanUpAsync(CancellationToken ct = default) => cleaner.ForgetOldAsync(clock.GetUtcNow().UtcDateTime, ct);
}
