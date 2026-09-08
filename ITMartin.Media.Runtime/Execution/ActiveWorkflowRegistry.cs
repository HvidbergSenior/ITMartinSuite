using System.Collections.Concurrent;

namespace ITMartin.Media.Runtime.Execution;

// Which workflow ids are executing in THIS process right now.
//
// Exists because "Running" in the database cannot answer that question:
// EfWorkflowInstanceStore.GetRecoverableWorkflowIdsAsync treats every
// Running row as recoverable, so WorkflowRecoveryHostedService (which
// re-checks every 5s) would pick up a workflow the queue consumer had
// only just started and launch a SECOND concurrent execution of the same
// workflow id as soon as that workflow saved its first checkpoint.
//
// Observed live on the ToshibaTest run 2026-09-08: workflow
// 9094af3d-9f66-4f41-b4f7-7c46c00c0373 ran twice at once in one worker,
// two FileDiscovery passes finishing 51 seconds apart with different file
// counts (86,508 vs 86,509), both writing the same output folder and both
// saving checkpoints under the same id - each demoting the other's
// IsLatest. That contention, not SQLite itself, is what produced the
// "database is locked" stall this run was restarted to escape.
public sealed class ActiveWorkflowRegistry
{
    private readonly ConcurrentDictionary<Guid, byte> _active = new();

    public bool TryEnter(Guid workflowId) =>
        _active.TryAdd(workflowId, 0);

    public void Exit(Guid workflowId) =>
        _active.TryRemove(workflowId, out _);

    public bool IsActive(Guid workflowId) =>
        _active.ContainsKey(workflowId);
}
