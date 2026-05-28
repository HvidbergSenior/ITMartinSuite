using ITMartinBudget.Application.Helpers;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Rules;
using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Services;

public class TransactionCategorizer
    : ITransactionCategorizer
{
    public void Categorize(
        BankTransaction transaction)
    {
        var match =
            TransactionRuleMatcher.FindBestMatch(
                transaction.NormalizedDescription,
                TransactionRules.Rules);

        if (match is null)
        {
            return;
        }

        transaction.Title =
            match.Title;

        transaction.Category =
            match.Category;

        transaction.BudgetGroup =
            match.BudgetGroup;

        transaction.TransactionType =
            match.TransactionType;

        transaction.IsRecurring =
            match.IsRecurring;
    }
}