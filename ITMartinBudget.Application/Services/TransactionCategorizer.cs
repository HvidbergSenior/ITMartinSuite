using System.Text.RegularExpressions;
using ITMartinBudget.Application.Helpers;
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
            TransactionNormalizer.Normalize(
                tx.Description);

        tx.TransactionType =
            tx.Amount >= 0
                ? TransactionType.Indkomst
                : TransactionType.Udgift;

        var matchingRules =
            TransactionRules.Rules

                .Where(x =>
                    x.TransactionType == null
                    || x.TransactionType ==
                    tx.TransactionType)

                .Where(x =>
                    Matches(
                        tx.NormalizedDescription,
                        x.Pattern,
                        x.ComparingType))

                .OrderByDescending(x =>
                    x.ComparingType ==
                    ComparingType.Exact)

                .ThenByDescending(x =>
                    TransactionNormalizer.Normalize(
                        x.Pattern).Length)

                .ToList();

        // =====================================
        // DEBUG
        // =====================================

        if (matchingRules.Count > 1)
        {
            Console.ForegroundColor =
                ConsoleColor.Yellow;

            Console.WriteLine(
                "=================================");

            Console.WriteLine(
                $"MULTIPLE MATCHES: {tx.Description}");

            Console.WriteLine(
                $"NORMALIZED: {tx.NormalizedDescription}");

            foreach (var item in matchingRules)
            {
                Console.WriteLine(
                    $"MATCH: {item.Pattern} => {item.Title}");
            }

            Console.WriteLine(
                "=================================");

            Console.ResetColor();
        }

        var rule =
            matchingRules
                .FirstOrDefault();

        // =====================================
        // MATCHED
        // =====================================

        if (rule is not null)
        {
            Console.ForegroundColor =
                ConsoleColor.Green;

            Console.WriteLine(
                $"MATCHED: {tx.Description}");

            Console.WriteLine(
                $"NORMALIZED: {tx.NormalizedDescription}");

            Console.WriteLine(
                $"RULE: {rule.Pattern}");

            Console.WriteLine(
                $"TITLE: {rule.Title}");

            Console.WriteLine(
                $"CATEGORY: {rule.Category}");

            Console.WriteLine(
                $"BUDGET GROUP: {rule.BudgetGroup}");

            Console.WriteLine(
                "---------------------------------");

            Console.ResetColor();

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
        // UNCATEGORIZED
        // =====================================

        Console.ForegroundColor =
            ConsoleColor.Red;

        Console.WriteLine(
            "#################################");

        Console.WriteLine(
            $"UNCATEGORIZED: {tx.Description}");

        Console.WriteLine(
            $"NORMALIZED: {tx.NormalizedDescription}");

        Console.WriteLine(
            $"AMOUNT: {tx.Amount}");

        Console.WriteLine(
            $"TYPE: {tx.TransactionType}");

        Console.WriteLine(
            "#################################");

        Console.ResetColor();

        tx.Category =
            Category.Andet;

        tx.BudgetGroup =
            BudgetGroup.Uncategorized;

        tx.Title =
            tx.TransactionType ==
            TransactionType.Indkomst
                ? "Ukategoriseret Indkomst"
                : "Ukategoriseret";

        tx.IsRecurring = false;
    }

    private bool Matches(
        string input,
        string pattern,
        ComparingType comparingType)
    {
        pattern =
            TransactionNormalizer.Normalize(
                pattern);

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