using System.Collections.Concurrent;

namespace ITMartinClub.Server.Services;

// Guards Admin.razor's PIN unlock screen against brute-forcing - it used to
// have no throttling at all (a wrong guess just flipped a bool, unlimited
// retries). In-memory only, keyed by GroupId: a container restart clears it,
// which is fine since the threat model here is "many guesses in a short
// burst," not a determined attacker who can wait out a restart.
public sealed class AdminPinRateLimiterService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<Guid, Queue<DateTime>> _failuresByGroup = new();

    public bool IsLockedOut(Guid groupId)
    {
        if (!_failuresByGroup.TryGetValue(groupId, out var failures)) return false;
        lock (failures)
        {
            Prune(failures);
            return failures.Count >= MaxAttempts;
        }
    }

    public void RegisterFailure(Guid groupId)
    {
        var failures = _failuresByGroup.GetOrAdd(groupId, _ => new Queue<DateTime>());
        lock (failures)
        {
            Prune(failures);
            failures.Enqueue(DateTime.UtcNow);
        }
    }

    public void RegisterSuccess(Guid groupId) => _failuresByGroup.TryRemove(groupId, out _);

    private static void Prune(Queue<DateTime> failures)
    {
        var cutoff = DateTime.UtcNow - LockoutWindow;
        while (failures.Count > 0 && failures.Peek() < cutoff)
            failures.Dequeue();
    }
}
