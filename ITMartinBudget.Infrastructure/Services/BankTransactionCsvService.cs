using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using ITMartinBudget.Application.Helpers;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Services;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;
using ITMartinBudget.Infrastructure.Csv;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBudget.Infrastructure.Services;

public class BankTransactionCsvService
{
    private readonly BudgetDbContext _db;
    private readonly ITransactionCategorizer _categorizer;
    private readonly IClaudeTransactionCategorizationService _claude;

    public BankTransactionCsvService(
        BudgetDbContext db,
        ITransactionCategorizer categorizer,
        IClaudeTransactionCategorizationService claude)
    {
        _db = db;
        _categorizer = categorizer;
        _claude = claude;
    }

    public async Task<List<BankTransaction>> ImportAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        using var reader =
            new StreamReader(stream);

        var config =
            new CsvConfiguration(
                new CultureInfo("da-DK"))
            {
                Delimiter = ";",
                MissingFieldFound = null,

                BadDataFound = x =>
                {
                    Console.WriteLine(
                        $"BAD DATA: {x.RawRecord}");
                }
            };

        using var csv =
            new CsvReader(reader, config);

        csv.Context.RegisterClassMap<
            BankTransactionMap>();

        var records =
            new List<BankTransaction>();

        await foreach (var record in
                       csv.GetRecordsAsync<BankTransaction>())
        {
            // DEBUG
            if (record.Description.Contains(
                    "Alka",
                    StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    $"RAW: {record.Date:d} | {record.Description} | {record.Amount}");
            }

            // Original description
            record.Description =
                record.Description?.Trim()
                ?? string.Empty;
            // Original description
            record.Description =
                record.Description?.Trim()
                ?? string.Empty;

            // Normalized description
            record.NormalizedDescription =
                TransactionNormalizer.Normalize(
                    record.Description);
            // Transaction type
            record.TransactionType =
                record.Amount < 0
                    ? TransactionType.Udgift
                    : TransactionType.Indkomst;

            // Defaults
            record.Category =
                Category.Andet;

            record.BudgetGroup =
                BudgetGroup.Unknown;

            record.RecurringIntervalMonths = 0;

            // Categorize
            _categorizer.Categorize(record);

            // Final fallback
            if (record.BudgetGroup ==
                BudgetGroup.Unknown)
            {
                record.BudgetGroup =
                    BudgetGroup.Uncategorized;

                record.Title =
                    record.Amount >= 0
                        ? "Ukategoriseret Indkomst"
                        : "Ukategoriseret";

                record.Category =
                    Category.Andet;
            }

            records.Add(record);
        }

        // Claude AI categorization for uncategorized transactions
        await CategorizeWithClaudeAsync(records, cancellationToken);

        // Analysis BEFORE deduplication
        var analysis =
            CategorizationAnalysisService
                .Analyze(records);

        CategorizationAnalysisService
            .PrintToConsole(analysis);

        var existingKeys =
            await GetExistingKeys();

        var newTransactions =
            Deduplicate(
                records,
                existingKeys);

        if (newTransactions.Any())
        {
            await _db.Transactions
                .AddRangeAsync(newTransactions);
        }

        await _db.SaveChangesAsync();

        Console.WriteLine("");
        Console.WriteLine(
            $"Imported {newTransactions.Count} new transactions");

        return newTransactions;
    }

    private async Task<HashSet<string>>
        GetExistingKeys()
    {
        return (await _db.Transactions

                .Where(x => x.LedgerId == "family")

                .Select(x => new
                {
                    x.Date,
                    x.Amount,
                    x.NormalizedDescription
                })

                .ToListAsync())

            .Select(x => CreateKey(
                x.Date,
                x.Amount,
                x.NormalizedDescription))

            .ToHashSet();
    }

    private List<BankTransaction> Deduplicate(
        List<BankTransaction> records,
        HashSet<string> existingKeys)
    {
        return records

            .Where(x =>
                !existingKeys.Contains(
                    CreateKey(
                        x.Date,
                        x.Amount,
                        x.NormalizedDescription)))

            .ToList();
    }

    private string CreateKey(
        DateTime date,
        decimal amount,
        string description)
    {
        return
            $"{date:yyyyMMdd}-{amount:F2}-{description}";
    }

    private async Task CategorizeWithClaudeAsync(
        List<BankTransaction> records,
        CancellationToken cancellationToken)
    {
        var uncategorized = records
            .Where(x => x.BudgetGroup == BudgetGroup.Uncategorized)
            .ToList();

        if (!uncategorized.Any())
            return;

        Console.WriteLine($"Sending {uncategorized.Count} uncategorized transactions to Claude...");

        foreach (var tx in uncategorized)
        {
            try
            {
                var result = await _claude.CategorizeAsync(
                    tx.Description,
                    tx.Amount,
                    cancellationToken);

                tx.Title = result.Title;
                tx.Category = result.Category;
                tx.BudgetGroup = result.BudgetGroup;
                tx.RecurringIntervalMonths = result.RecurringIntervalMonths;

                Console.WriteLine($"  Claude: \"{tx.Description}\" → {result.BudgetGroup} / {result.Title}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Claude failed for \"{tx.Description}\": {ex.Message}");
            }
        }
    }
}