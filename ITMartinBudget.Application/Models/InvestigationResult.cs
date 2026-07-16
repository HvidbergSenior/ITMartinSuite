namespace ITMartinBudget.Application.Models;

public sealed record InvestigationResult(
    string Reasoning,
    string SuggestedScope, // "Business", "Private", or "Unsure"
    string Confidence); // "High", "Medium", "Low"
