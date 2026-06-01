using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Interfaces;

public interface IFamilyBudgetOverviewService
{
    FamilyPlanningViewModel Build2025Overview(
        List<BankTransaction> transactions);

    FamilyPlanningViewModel Build2026FirstHalfOverview(
        List<BankTransaction> transactions);

    FamilyPlanningViewModel Build2026SecondHalfOverview(
        List<BankTransaction> transactions);
}