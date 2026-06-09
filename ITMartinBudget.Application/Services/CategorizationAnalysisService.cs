using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Services;

public static class CategorizationAnalysisService
{
    public static CategorizationResult Analyze(
        List<BankTransaction> transactions)
    {
        var uncategorizedTransactions =

            transactions

                .Where(x =>
                    x.BudgetGroup ==
                    BudgetGroup.Uncategorized)

                .ToList();

        var uncategorized =

            uncategorizedTransactions

                .GroupBy(x =>
                    x.NormalizedDescription)

                .Select(x =>
                    new UncategorizedTransaction
                    {
                        Description =
                            x.Key,

                        Count =
                            x.Count(),

                        TotalAmount =
                            x.Sum(t =>
                                Math.Abs(
                                    t.Amount)),

                        Examples =
                            x.Take(5)

                                .Select(t =>
                                    t.Description)

                                .Distinct()

                                .ToList()
                    })

                .OrderByDescending(x =>
                    x.Count)

                .ThenByDescending(x =>
                    x.TotalAmount)

                .ToList();

        var categorizedCount =
            transactions.Count -
            uncategorizedTransactions.Count;

        var coverage =
            transactions.Count == 0
                ? 0
                : Math.Round(
                    (
                        decimal)categorizedCount
                        / transactions.Count
                        * 100m,
                    2);
        foreach (var tx in uncategorizedTransactions
                     .Where(x =>
                         x.NormalizedDescription ==
                         "alka forsikring"))
        {
            Console.WriteLine(
                $"{tx.Date:d} | " +
                $"{tx.Description} | " +
                $"{tx.Amount}");
        }
        return new()
        {
            Total =
                transactions.Count,

            Categorized =
                categorizedCount,

            Uncategorized =
                uncategorizedTransactions.Count,

            CoveragePercentage =
                coverage,

            UncategorizedAmount =

                uncategorizedTransactions

                    .Sum(x =>
                        Math.Abs(
                            x.Amount)),

            UncategorizedTransactions =
                uncategorized
        };
    }

    public static void PrintToConsole(
        CategorizationResult analysis)
    {
        Console.WriteLine("");
        Console.WriteLine(
            "=================================");

        Console.WriteLine(
            "CATEGORIZATION ANALYSIS");

        Console.WriteLine(
            "=================================");

        Console.WriteLine(
            $"Coverage: {analysis.CoveragePercentage}%");

        Console.WriteLine(
            $"Categorized: {analysis.Categorized}");

        Console.WriteLine(
            $"Uncategorized: {analysis.Uncategorized}");

        Console.WriteLine(
            $"Uncategorized Amount: {analysis.UncategorizedAmount}");

        Console.WriteLine("");

        foreach (var item in
                 analysis.UncategorizedTransactions)
        {
            Console.WriteLine(
                "---------------------------------");

            Console.WriteLine(
                $"Normalized: {item.Description}");

            Console.WriteLine(
                $"Count: {item.Count}");

            Console.WriteLine(
                $"Total: {item.TotalAmount}");

            foreach (var example in
                     item.Examples)
            {
                Console.WriteLine(
                    $"Example: {example}");
            }
        }

        Console.WriteLine("");
        Console.WriteLine(
            "=================================");
    }
}