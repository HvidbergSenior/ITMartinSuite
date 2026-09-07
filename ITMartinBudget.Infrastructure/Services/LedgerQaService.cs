using Anthropic;
using Anthropic.Models.Messages;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ITMartinBudget.Infrastructure.Services;

// Backs the "❓ Spørg" box on /shop-overview. Rather than let Claude reason
// over (or hallucinate about) thousands of raw rows, we pre-aggregate a
// compact monthly + per-category digest server-side - the same numbers a
// human would compute by hand - and Claude only answers from that summary.
// Keeps token cost tiny and keeps numeric answers grounded in real sums
// instead of the model's own arithmetic over a huge transaction dump.
public sealed class LedgerQaService : ILedgerQaService
{
    private const string SystemPrompt = """
        You answer questions about a Danish bank ledger (personal or a small
        shop's account, sometimes mixing business and private spending) using
        ONLY the monthly and category summary data given to you - never
        invent numbers not derivable from it. If the question can't be
        answered from this summary (e.g. it needs a specific single
        transaction's raw text, or asks about a period outside the data
        range), say so plainly instead of guessing. Be concise - a couple of
        sentences or a short list, with exact kr. amounts. Always answer in
        Danish.

        Every category line is tagged [Business] or [Private] - NEVER add or
        compare a [Business] figure together with a [Private] one as if they
        were the same thing, even when the category names look similar or
        identical. Keeping business and private money separate is the entire
        purpose of this ledger; if a question is ambiguous about which scope
        it means, ask which one instead of guessing or silently combining
        both.
        """;

    private readonly BudgetDbContext _db;
    private readonly AnthropicClient _client;

    public LedgerQaService(BudgetDbContext db, IConfiguration configuration)
    {
        _db = db;
        var apiKey = configuration["Claude:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing Claude:ApiKey configuration");

        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<string> AskAsync(string ledgerId, string question, CancellationToken cancellationToken = default)
    {
        var transactions = await _db.Transactions
            .Where(x => x.LedgerId == ledgerId)
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

        if (transactions.Count == 0)
            return "Der er ingen transaktioner for denne konto endnu.";

        var digest = BuildDigest(transactions);

        var request = new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 500,
            System = SystemPrompt,
            Messages = [new() { Role = Role.User, Content = $"{digest}\n\nSpørgsmål: {question}" }]
        };

        var response = await _client.Messages.Create(request, cancellationToken);

        var text = response.Content
            .Select(block => block.TryPickText(out var t) ? t.Text : null)
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));

        return text ?? "Kunne ikke generere et svar.";
    }

    // Public static (not private) so the digest content is directly
    // unit-testable without needing a real/mocked AnthropicClient - matches
    // this codebase's convention for other pure-logic helpers
    // (CategoryNameCleaner, CategoryDuplicateFinder, ...).
    public static string BuildDigest(List<Domain.Entities.BankTransaction> transactions)
    {
        var lines = new List<string>
        {
            $"Periode: {transactions.First().Date:yyyy-MM-dd} til {transactions.Last().Date:yyyy-MM-dd} ({transactions.Count} transaktioner i alt).",
            "",
            "Måned for måned (forretning omsætning / forretning udgifter / privat ud / privat ind, alle i DKK):"
        };

        var monthly = transactions
            .GroupBy(x => new DateTime(x.Date.Year, x.Date.Month, 1))
            .OrderBy(g => g.Key);

        foreach (var m in monthly)
        {
            var biz = m.Where(x => x.Scope == TransactionScope.Business).ToList();
            var priv = m.Where(x => x.Scope == TransactionScope.Private).ToList();
            var revenue = biz.Where(x => x.Amount > 0).Sum(x => x.Amount);
            var expenses = biz.Where(x => x.Amount < 0).Sum(x => x.Amount);
            var privOut = priv.Where(x => x.Amount < 0).Sum(x => x.Amount);
            var privIn = priv.Where(x => x.Amount > 0).Sum(x => x.Amount);
            lines.Add($"- {m.Key:yyyy-MM}: {revenue:F0} / {expenses:F0} / {privOut:F0} / {privIn:F0}");
        }

        lines.Add("");
        lines.Add("Kategorier (navn [scope]: antal poster, samlet beløb DKK):");

        // Grouped by (name, scope) TOGETHER, never name alone - "man skal
        // ikke bruge transaktioner fra forretning i Privat og omvendt"
        // (2026-09-07). A category name is occasionally reused across both
        // scopes (or a merge can leave one "Blandet") - summing those
        // together under one line would silently blend business and private
        // money, exactly what this whole ledger feature exists to keep
        // apart (see ProblemPurposeBanner on /shop-overview). Every section
        // below keys on this same (name, scope) pair.
        var byCategory = transactions
            .Where(x => x.UserCategoryName != null)
            .GroupBy(x => (Name: x.UserCategoryName!, x.Scope))
            .Select(g => new
            {
                g.Key.Name,
                g.Key.Scope,
                Count = g.Count(),
                Sum = g.Sum(x => x.Amount),
            })
            .OrderByDescending(c => Math.Abs(c.Sum))
            .ToList();

        foreach (var c in byCategory)
            lines.Add($"- {c.Name} [{c.Scope}]: {c.Count} stk., {c.Sum:F0} kr.");

        // Neither view above crosses category × year - "check Husleje for
        // 2025 vs 2026, what was the increase" (2026-09-07) genuinely
        // couldn't be answered from monthly-totals-across-all-categories
        // plus category-totals-across-the-whole-period alone. Per-year, not
        // per-month, to keep this compact - a category only needs a row per
        // year it actually has activity in, not one per calendar month.
        var yearsPresent = transactions.Select(x => x.Date.Year).Distinct().OrderBy(y => y).ToList();
        if (yearsPresent.Count > 1)
        {
            lines.Add("");
            lines.Add("Kategorier pr. år (navn [scope]: år: beløb DKK, kun år hvor kategorien har posteringer):");

            var byCategoryAndYear = transactions
                .Where(x => x.UserCategoryName != null)
                .GroupBy(x => (Name: x.UserCategoryName!, x.Scope))
                .OrderByDescending(g => Math.Abs(g.Sum(x => x.Amount)));

            foreach (var cat in byCategoryAndYear)
            {
                var perYear = cat
                    .GroupBy(x => x.Date.Year)
                    .OrderBy(g => g.Key)
                    .Select(g => $"{g.Key}: {g.Sum(x => x.Amount):F0} kr.");
                lines.Add($"- {cat.Key.Name} [{cat.Key.Scope}]: {string.Join(", ", perYear)}");
            }
        }

        // "Hvad har stigningen været månedlig" (2026-09-07) - a category's
        // per-year total still can't answer "which months", and dumping
        // every category's full month-by-month history would blow up the
        // digest (70+ categories x up to 20 months). Bounded to the top 15
        // (name, scope) pairs by absolute total - the ones actually
        // big/recurring enough (Husleje, Abonnementer, ...) to plausibly be
        // asked about month-by-month; a small one-off category was never
        // going to get a "how has this trended monthly" question anyway.
        const int MaxCategoriesForMonthlyDetail = 15;
        var topCategoriesByMonth = transactions
            .Where(x => x.UserCategoryName != null)
            .GroupBy(x => (Name: x.UserCategoryName!, x.Scope))
            .OrderByDescending(g => Math.Abs(g.Sum(x => x.Amount)))
            .Take(MaxCategoriesForMonthlyDetail)
            .ToList();

        if (topCategoriesByMonth.Any(cat => cat.Select(x => new DateTime(x.Date.Year, x.Date.Month, 1)).Distinct().Count() > 1))
        {
            lines.Add("");
            lines.Add($"De {MaxCategoriesForMonthlyDetail} største kategorier, pr. måned (navn [scope]: yyyy-MM: beløb DKK, kun måneder med posteringer):");

            foreach (var cat in topCategoriesByMonth)
            {
                var perMonth = cat
                    .GroupBy(x => new DateTime(x.Date.Year, x.Date.Month, 1))
                    .OrderBy(g => g.Key)
                    .Select(g => $"{g.Key:yyyy-MM}: {g.Sum(x => x.Amount):F0} kr.");
                lines.Add($"- {cat.Key.Name} [{cat.Key.Scope}]: {string.Join(", ", perMonth)}");
            }
        }

        return string.Join('\n', lines);
    }
}
