using System.Text.RegularExpressions;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Services;

public class TransactionCategorizer
    : ITransactionCategorizer
{
    public void Categorize(
        BankTransaction tx)
    {
        tx.NormalizedDescription =
            Normalize(tx.Description);

        tx.TransactionType =
            tx.Amount >= 0
                ? TransactionType.Indkomst
                : TransactionType.Udgift;

        var rule =
            TransactionRules.Rules

                .OrderByDescending(x =>
                    x.Pattern.Length)

                .FirstOrDefault(x =>

                    tx.NormalizedDescription
                        .Contains(x.Pattern)

                    &&

                    (
                        x.TransactionType == null
                        || x.TransactionType ==
                        tx.TransactionType
                    ));

        if (rule is not null)
        {
            tx.Category =
                rule.Category;

            tx.BudgetGroup =
                rule.BudgetGroup;

            tx.Title =
                rule.Title;

            tx.IsRecurring =
                rule.IsRecurring;

            return;
        }

        tx.Category =
            Category.Other;

        if (tx.TransactionType ==
            TransactionType.Indkomst)
        {
            tx.BudgetGroup =
                BudgetGroup.VariableIncome;

            tx.Title =
                "Variable Income";
        }
        else
        {
            tx.BudgetGroup =
                BudgetGroup.VariableExpense;

            tx.Title =
                "Variable Expense";
        }

        tx.IsRecurring = false;
    }

    private string Normalize(
        string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        input =
            input.ToLowerInvariant();

        input = input

            .Replace("æ", "ae")
            .Replace("ø", "oe")
            .Replace("å", "aa");

        input = Regex.Replace(
            input,
            @"[^\w\s]",
            " ");

        input = Regex.Replace(
            input,
            @"\s+",
            " ");

        return input.Trim();
    }
}