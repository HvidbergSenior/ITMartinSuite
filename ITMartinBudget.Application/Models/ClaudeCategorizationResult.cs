using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Models;

public record ClaudeCategorizationResult(
    string Title,
    Category Category,
    BudgetGroup BudgetGroup,
    decimal RecurringIntervalMonths);
