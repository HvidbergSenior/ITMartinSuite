using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Services;

public static class CategorizationAnalysisService
{
    public static CategorizationResult Analyze(
        List<BankTransaction> transactions)
    {
        var uncategorized =
            transactions

                .Where(x =>
                    x.Category ==
                    Category.Andet)

                .GroupBy(x =>
                    x.NormalizedDescription)

                .Select(x =>
                    new UncategorizedTransaction
                    {
                        Description = x.Key,

                        Count = x.Count(),

                        TotalAmount =
                            x.Sum(t =>
                                Math.Abs(t.Amount))
                    })

                .OrderByDescending(x =>
                    x.Count)

                .ToList();

        return new()
        {
            Total = transactions.Count,

            Categorized =
                transactions.Count(x =>
                    x.Category !=
                    Category.Andet),

            Uncategorized =
                transactions.Count(x =>
                    x.Category ==
                    Category.Andet),

            UncategorizedAmount =
                transactions

                    .Where(x =>
                        x.Category ==
                        Category.Andet)

                    .Sum(x =>
                        Math.Abs(x.Amount)),

            UncategorizedTransactions =
                uncategorized
        };
    }
}