using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Interfaces;

public interface IFamilyBudgetOverviewService
{
    FamilyBudgetOverview Build2025Overview(
        List<BankTransaction> transactions);

    FamilyBudgetOverview Build2026FirstHalfOverview(
        List<BankTransaction> transactions);

    FamilyBudgetOverview Build2026SecondHalfOverview(
        List<BankTransaction> transactions);
}