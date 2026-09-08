using FluentAssertions;
using ITMartin.Media.Runtime.Execution;

namespace ITMartinFileSorter.Tests.QuickSortTests;

// Guards the fix for the ToshibaTest 2026-09-08 duplicate-execution
// incident - see ActiveWorkflowRegistry for the full account. The single
// invariant that matters: one workflow id can only be entered once until
// it is exited, no matter how many callers race for it.
[TestFixture]
public class ActiveWorkflowRegistryTests
{
    [Test]
    public void First_caller_enters_and_second_is_refused()
    {
        var registry = new ActiveWorkflowRegistry();
        var workflowId = Guid.NewGuid();

        registry.TryEnter(workflowId).Should().BeTrue();
        registry.TryEnter(workflowId).Should().BeFalse();
        registry.IsActive(workflowId).Should().BeTrue();
    }

    [Test]
    public void Workflow_can_be_entered_again_after_exit()
    {
        var registry = new ActiveWorkflowRegistry();
        var workflowId = Guid.NewGuid();

        registry.TryEnter(workflowId);
        registry.Exit(workflowId);

        registry.IsActive(workflowId).Should().BeFalse();
        registry.TryEnter(workflowId).Should().BeTrue();
    }

    [Test]
    public void Different_workflows_do_not_block_each_other()
    {
        var registry = new ActiveWorkflowRegistry();

        registry.TryEnter(Guid.NewGuid()).Should().BeTrue();
        registry.TryEnter(Guid.NewGuid()).Should().BeTrue();
    }

    [Test]
    public void Exactly_one_of_many_concurrent_callers_wins()
    {
        var registry = new ActiveWorkflowRegistry();
        var workflowId = Guid.NewGuid();
        var winners = 0;

        Parallel.For(0, 64, _ =>
        {
            if (registry.TryEnter(workflowId))
            {
                Interlocked.Increment(ref winners);
            }
        });

        winners.Should().Be(1);
    }

    [Test]
    public void Unknown_workflow_is_not_active()
    {
        new ActiveWorkflowRegistry()
            .IsActive(Guid.NewGuid())
            .Should().BeFalse();
    }
}
