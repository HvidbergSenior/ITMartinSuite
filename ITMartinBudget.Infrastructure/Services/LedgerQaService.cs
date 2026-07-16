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

    private static string BuildDigest(List<Domain.Entities.BankTransaction> transactions)
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
        lines.Add("Kategorier (navn: antal poster, samlet beløb DKK, scope):");

        var byCategory = transactions
            .Where(x => x.UserCategoryName != null)
            .GroupBy(x => x.UserCategoryName!)
            .Select(g => new
            {
                Name = g.Key,
                Count = g.Count(),
                Sum = g.Sum(x => x.Amount),
                Scope = g.Select(x => x.Scope).Distinct().Count() > 1 ? "Blandet" : g.First().Scope.ToString()
            })
            .OrderByDescending(c => Math.Abs(c.Sum));

        foreach (var c in byCategory)
            lines.Add($"- {c.Name}: {c.Count} stk., {c.Sum:F0} kr., {c.Scope}");

        return string.Join('\n', lines);
    }
}
