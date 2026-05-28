using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
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

    public BankTransactionCsvService(
        BudgetDbContext db,
        ITransactionCategorizer categorizer)
    {
        _db = db;
        _categorizer = categorizer;
    }

    public async Task<List<BankTransaction>> ImportAsync(
        Stream stream)
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
            // Original description
            record.Description =
                record.Description?.Trim()
                ?? string.Empty;

            // Normalized description
            record.NormalizedDescription =
                Normalize(record.Description);

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

            record.IsRecurring = false;

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

        // Analysis BEFORE deduplication
        var analysis =
            CategorizationAnalysisService
                .Analyze(records);

        Console.WriteLine("");
        Console.WriteLine("=================================");
        Console.WriteLine("CATEGORIZATION ANALYSIS");
        Console.WriteLine("=================================");

        Console.WriteLine(
            $"Total: {analysis.Total}");

        Console.WriteLine(
            $"Categorized: {analysis.Categorized}");

        Console.WriteLine(
            $"Uncategorized: {analysis.Uncategorized}");

        Console.WriteLine(
            $"Uncategorized Amount: {analysis.UncategorizedAmount:N2} kr");

        Console.WriteLine("");
        Console.WriteLine(
            "TOP UNCATEGORIZED:");

        foreach (var item in
                 analysis.UncategorizedTransactions
                     .Take(50))
        {
            Console.WriteLine(
                $"{item.Count}x | " +
                $"{item.TotalAmount:N2} kr | " +
                $"{item.Description}");
        }

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

    private string Normalize(
        string? input)
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

        // Remove numbers
        input = Regex.Replace(
            input,
            @"\d+",
            " ");

        // Remove punctuation
        input = Regex.Replace(
            input,
            @"[^\w\s]",
            " ");

        // Collapse spaces
        input = Regex.Replace(
            input,
            @"\s+",
            " ");

        return input.Trim();
    }
}