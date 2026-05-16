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
        // =====================================
        // NORMALIZE
        // =====================================

        tx.NormalizedDescription =
            Normalize(tx.Description);

        // =====================================
        // TRANSACTION TYPE
        // =====================================

        tx.TransactionType =
            tx.Amount >= 0
                ? TransactionType.Indkomst
                : TransactionType.Udgift;

        // =====================================
        // MATCH RULE
        // LONGEST PATTERN FIRST
        // =====================================

        var rule =
            TransactionRules.Rules

                .OrderByDescending(x =>
                    x.Pattern.Length)

                .FirstOrDefault(x =>
                    tx.NormalizedDescription
                        .Contains(x.Pattern));

        // =====================================
        // RULE MATCHED
        // =====================================

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

        // =====================================
        // FALLBACK
        // =====================================

        tx.Category =
            Category.Other;

        // =====================================
        // DEFAULT GROUPS
        // =====================================

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

    // =====================================
    // NORMALIZE
    // =====================================

    private string Normalize(
        string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        input =
            input.ToLowerInvariant();

        // =====================================
        // DANISH CHARS
        // =====================================

        input = input

            .Replace("æ", "ae")
            .Replace("ø", "oe")
            .Replace("å", "aa");

        // =====================================
        // REMOVE SPECIAL CHARS
        // =====================================

        input = Regex.Replace(
            input,
            @"[^\w\s]",
            " ");

        // =====================================
        // REMOVE EXTRA SPACES
        // =====================================

        input = Regex.Replace(
            input,
            @"\s+",
            " ");

        return input.Trim();
    }
}