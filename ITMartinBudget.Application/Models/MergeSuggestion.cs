namespace ITMartinBudget.Application.Models;

public sealed record MergeSuggestion(
    List<string> SourceNames,
    string SuggestedTargetName,
    string Reasoning);
