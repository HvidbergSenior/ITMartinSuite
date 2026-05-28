using System.Text.RegularExpressions;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Rules;
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

                    Matches(
                        tx.NormalizedDescription,
                        x.Pattern,
                        x.ComparingType)

                    &&

                    (
                        x.TransactionType == null
                        || x.TransactionType ==
                        tx.TransactionType
                    ));

        if (rule is not null)
        {
            Console.WriteLine(
                $"{tx.Description} matched {rule.Pattern} => {rule.BudgetGroup}");

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
            Category.Andet;

        if (tx.TransactionType ==
            TransactionType.Indkomst)
        {
            tx.BudgetGroup =
                BudgetGroup.Uncategorized;

            tx.Title =
                "Ukategoriseret Indkomst";
        }
        else
        {
            tx.BudgetGroup =
                BudgetGroup.Uncategorized;

            tx.Title =
                "Ukategoriseret";
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

    private bool Matches(
        string input,
        string pattern,
        ComparingType comparingType)
    {
        pattern =
            Normalize(pattern);

        return comparingType switch
        {
            ComparingType.Exact =>

                input == pattern,

            ComparingType.Word =>

                Regex.IsMatch(
                    input,
                    $@"\b{Regex.Escape(pattern)}\b"),

            _ =>

                input.Contains(pattern)
        };
    }
}