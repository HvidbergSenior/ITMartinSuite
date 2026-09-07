using FluentAssertions;
using ITMartinBudget.Application.Helpers;

namespace ITMartinBudget.Tests.Helpers;

// Documents (and enforces) the exact sequence every Bogshoppen-style ledger
// goes through - same spirit as FileSorter's QuickSortStepOrderTests: the
// step order lives in one place (LedgerWorkflowSteps), StepNav.razor just
// renders it, and this test is what stops the two from silently drifting
// apart.
[TestFixture]
public class LedgerWorkflowStepsTests
{
    // The authoritative order. If this list and LedgerWorkflowSteps.Steps
    // ever disagree, one of them is wrong - update whichever one doesn't
    // match the *actual intended* flow, don't just make the test pass.
    private static readonly (string Label, string RoutePrefix)[] ExpectedSteps =
    [
        ("Upload", "/shop-upload"),           // 1. Get a bank CSV into the ledger.
        ("Kategoriser", "/shop-categorize"),  // 2. Tag each recurring transaction pattern Business/Private + a category name.
        ("Flet", "/shop-categories"),         // 3. Merge near-duplicate categories (Shell/Q8/Uno-X -> Benzin) into one.
        ("Overblik", "/shop-overview"),       // 4. See the result - income/spend, Business vs Private (added 2026-09-07).
    ];

    [Test]
    public void Step_order_matches_the_documented_sequence()
    {
        LedgerWorkflowSteps.Steps
            .Select(s => (s.Label, s.RoutePrefix))
            .Should().Equal(ExpectedSteps,
                "LedgerWorkflowSteps.Steps has changed - update ExpectedSteps above to match " +
                "the new intended flow, or fix the reorder if it was accidental.");
    }

    [Test]
    public void There_are_exactly_four_steps()
    {
        // Was 3 (Upload -> Kategoriser -> Flet) - Overblik added 2026-09-07
        // as the "see the result" step, once everything's categorized and
        // merged. StepNav.razor's rendering (arrows, done/current styling)
        // is already generic over LedgerWorkflowSteps.Steps.Count, so this
        // needed no template change, just the new entry above.
        LedgerWorkflowSteps.Steps.Should().HaveCount(4);
    }

    [TestCase("bogshoppen", "/shop-upload/bogshoppen")]
    [TestCase("hvidberg", "/shop-categorize/hvidberg")]
    [TestCase("has space", "/shop-categorize/has%20space")]
    public void BuildHref_escapes_the_ledger_id_into_the_steps_route(string ledgerId, string expectedSuffix)
    {
        var step = expectedSuffix.Contains("shop-upload")
            ? LedgerWorkflowSteps.Steps[0]
            : LedgerWorkflowSteps.Steps[1];

        LedgerWorkflowSteps.BuildHref(step, ledgerId).Should().Be(expectedSuffix);
    }
}
