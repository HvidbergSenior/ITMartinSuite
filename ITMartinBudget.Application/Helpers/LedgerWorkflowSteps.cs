namespace ITMartinBudget.Application.Helpers;

public sealed record LedgerWorkflowStep(string Label, string RoutePrefix);

// The exact, intended sequence every Bogshoppen-style ledger goes through -
// documented here (not just in StepNav.razor's markup) so it can't silently
// drift from what LedgerWorkflowStepsTests enforces. Mirrors the same
// "step order lives in one place, UI just renders it" split FileSorter uses
// for QuickSortWorkflowDefinition.
public static class LedgerWorkflowSteps
{
    public static readonly IReadOnlyList<LedgerWorkflowStep> Steps =
    [
        new("Upload", "/shop-upload"),
        new("Kategoriser", "/shop-categorize"),
        new("Flet", "/shop-categories"),
    ];

    public static string BuildHref(
        LedgerWorkflowStep step,
        string ledgerId)
    {
        return $"{step.RoutePrefix}/{Uri.EscapeDataString(ledgerId)}";
    }
}
