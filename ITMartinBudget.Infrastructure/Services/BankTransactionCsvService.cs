using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using ITMartinBudget.Application.Interfaces;
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
        using var reader = new StreamReader(stream);

        var config = new CsvConfiguration(
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

        csv.Context.RegisterClassMap<BankTransactionMap>();

        var records =
            new List<BankTransaction>();

        await foreach (var record in
                       csv.GetRecordsAsync<BankTransaction>())
        {
            // Keep original description
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

            // Default fallback
            record.Category =
                Category.Andet;

            record.IsRecurring = false;

            // Categorize
            _categorizer.Categorize(record);

            // Safety fallback
            if (record.BudgetGroup == 0)
            {
                record.BudgetGroup =
                    record.Amount >= 0
                        ? BudgetGroup.VariableIncome
                        : BudgetGroup.VariableExpense;
            }

            records.Add(record);
        }

        var existingKeys =
            await GetExistingKeys();

        var newTransactions =
            Deduplicate(records, existingKeys);

        if (newTransactions.Any())
        {
            await _db.Transactions
                .AddRangeAsync(newTransactions);
        }

        await _db.SaveChangesAsync();

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

        // remove numbers
        input = Regex.Replace(
            input,
            @"\d+",
            " ");

        // remove punctuation
        input = Regex.Replace(
            input,
            @"[^\w\s]",
            " ");

        // collapse spaces
        input = Regex.Replace(
            input,
            @"\s+",
            " ");

        return input.Trim();
    }
}