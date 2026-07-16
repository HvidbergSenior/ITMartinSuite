using ITMartinBudget.Application.Helpers;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;
using ITMartinBudget.Infrastructure.Csv;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBudget.Infrastructure.Services;

// Imports a bank export into a named ledger (Bogshoppen, Hvidberg, ITMartin,
// ...), kept entirely separate from any other ledger's transactions.
// Format-specific reading is delegated to IBankStatementParser
// implementations (RawBankStatementParser, TotalkontoParser, ...) - this
// class is the one shared pipeline every format funnels through afterwards:
// scope classification, category-rule application/seeding, dedup, save.
// Adding a new bank export shape later means writing one small parser, not
// duplicating this whole pipeline again.
public class LedgerImportService
{
    private readonly BudgetDbContext _db;
    private readonly ITransactionScopeClassifier _scopeClassifier;
    private readonly ITransactionCategorizer _categorizer;
    private readonly IReadOnlyList<IBankStatementParser> _parsers;

    public LedgerImportService(
        BudgetDbContext db,
        ITransactionScopeClassifier scopeClassifier,
        ITransactionCategorizer categorizer,
        IEnumerable<IBankStatementParser> parsers)
    {
        _db = db;
        _scopeClassifier = scopeClassifier;
        _categorizer = categorizer;
        // Order matters: specific/detectable shapes first, the raw
        // no-header format last since it has no signature of its own and
        // matches anything (see RawBankStatementParser.CanParse).
        _parsers = parsers.OrderBy(p => p is RawBankStatementParser ? 1 : 0).ToList();
    }

    public async Task<List<BankTransaction>> ImportAsync(
        Stream stream,
        string ledgerId,
        CancellationToken cancellationToken = default)
    {
        // Buffered so the file can be peeked (to pick a parser) and then
        // re-read from the start by that parser's own reader/encoding.
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var firstLine = await PeekFirstLineAsync(buffer);
        buffer.Position = 0;

        var parser = _parsers.First(p => p.CanParse(firstLine));
        var rows = await parser.ParseAsync(buffer, cancellationToken);

        // Rules the user already saved for this ledger (see CategoryRuleService)
        // - applied automatically so a recurring bill only needs categorizing
        // once, not on every future import.
        var savedRules = await _db.CategoryRules
            .Where(x => x.LedgerId == ledgerId)
            .ToDictionaryAsync(x => x.Pattern, cancellationToken);

        // Chosen once on /shop-upload the first time this ledger was created
        // (see LedgerConfig) - Both means "classify normally", a fixed value
        // means this ledger's account is genuinely single-purpose and there's
        // nothing ambiguous to guess at.
        var scopeMode = (await _db.LedgerConfigs.FindAsync(new object?[] { ledgerId }, cancellationToken))?.ScopeMode
            ?? LedgerScopeMode.Both;
        TransactionScope? fixedScope = scopeMode switch
        {
            LedgerScopeMode.PrivateOnly => TransactionScope.Private,
            LedgerScopeMode.BusinessOnly => TransactionScope.Business,
            _ => null,
        };

        var newRulesThisImport = new Dictionary<string, CategoryRule>();
        var records = new List<BankTransaction>();

        foreach (var row in rows)
        {
            var normalizedDescription = TransactionNormalizer.Normalize(row.Description);

            var tx = new BankTransaction
            {
                Date = row.Date,
                Description = row.Description,
                NormalizedDescription = normalizedDescription,
                Amount = row.Amount,
                Balance = row.Balance,
                TransactionType = row.Amount < 0 ? TransactionType.Udgift : TransactionType.Indkomst,
                Category = Category.Andet,
                BudgetGroup = BudgetGroup.Unknown,
                RecurringIntervalMonths = 0,
                LedgerId = ledgerId,
                RawDetails = row.RawDetails,
            };

            if (fixedScope.HasValue)
                tx.Scope = fixedScope.Value;
            else
                _scopeClassifier.Classify(tx);

            // Private spending on a mixed or private-only account is the same
            // kind of thing as family spending (groceries, insurance, phone,
            // rent) - reuse the same category rule engine the family budget
            // already has instead of inventing a second one.
            if (tx.Scope == TransactionScope.Private)
                _categorizer.Categorize(tx);

            // A saved rule is the user's own final word - overrides whatever
            // the auto classifier decided.
            if (savedRules.TryGetValue(normalizedDescription, out var rule))
            {
                tx.UserCategoryName = rule.CategoryName;
                tx.Scope = rule.Scope;
            }
            // No saved rule yet, but the source file already told us what
            // this is (e.g. Totalkonto's own Kategori column) - seed a real
            // CategoryRule from it now rather than leaving the row for
            // manual categorization the user would just re-type anyway.
            else if (row.SuggestedCategoryName is not null)
            {
                if (!newRulesThisImport.TryGetValue(normalizedDescription, out var newRule))
                {
                    newRule = new CategoryRule
                    {
                        LedgerId = ledgerId,
                        Pattern = normalizedDescription,
                        CategoryName = row.SuggestedCategoryName,
                        Scope = tx.Scope,
                    };
                    newRulesThisImport[normalizedDescription] = newRule;
                }
                tx.UserCategoryName = newRule.CategoryName;
            }

            records.Add(tx);
        }

        if (newRulesThisImport.Count > 0)
            await _db.CategoryRules.AddRangeAsync(newRulesThisImport.Values, cancellationToken);

        var existingKeys = await GetExistingKeys(ledgerId);
        var newTransactions = records
            .Where(x => !existingKeys.Contains(CreateKey(x.Date, x.Amount, x.NormalizedDescription)))
            .ToList();

        if (newTransactions.Count > 0 || newRulesThisImport.Count > 0)
        {
            if (newTransactions.Count > 0)
                await _db.Transactions.AddRangeAsync(newTransactions, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return newTransactions;
    }

    private static async Task<string> PeekFirstLineAsync(Stream stream)
    {
        // Latin1/ASCII decoding is enough just to detect a header signature
        // like "Dato;Tekst;" - every format seen so far uses plain ASCII for
        // its header text or its leading positional column, so this reads
        // correctly regardless of the file's real encoding (UTF-8 vs
        // Windows-1252), without needing to know that encoding yet.
        using var reader = new StreamReader(stream, System.Text.Encoding.Latin1, leaveOpen: true);
        return await reader.ReadLineAsync() ?? string.Empty;
    }

    private async Task<HashSet<string>> GetExistingKeys(string ledgerId)
    {
        return (await _db.Transactions
                .Where(x => x.LedgerId == ledgerId)
                .Select(x => new { x.Date, x.Amount, x.NormalizedDescription })
                .ToListAsync())
            .Select(x => CreateKey(x.Date, x.Amount, x.NormalizedDescription))
            .ToHashSet();
    }

    private static string CreateKey(DateTime date, decimal amount, string description) =>
        $"{date:yyyyMMdd}-{amount:F2}-{description}";
}
