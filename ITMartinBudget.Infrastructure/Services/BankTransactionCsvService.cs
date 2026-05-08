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

    private readonly ITransactionProcessor _processor;

    public BankTransactionCsvService(
        BudgetDbContext db,
        ITransactionProcessor processor)
    {
        _db = db;

        _processor = processor;
    }

    public async Task<List<BankTransaction>> ImportAsync(Stream stream)
    {
        using var reader = new StreamReader(stream);

        var config = new CsvConfiguration(
            new CultureInfo("da-DK"))
        {
            Delimiter = ";"
        };

        using var csv = new CsvReader(reader, config);

        csv.Context.RegisterClassMap<BankTransactionMap>();

        var records = new List<BankTransaction>();

        await foreach (var record in csv.GetRecordsAsync<BankTransaction>())
        {
            record.Description =
                Normalize(record.Description);

            record.Category =
                Category.Other;

            // ACCOUNTING TRUTH
            record.TransactionType =
                record.Amount < 0
                    ? TransactionType.Udgift
                    : TransactionType.Indkomst;

            await _processor.ProcessAsync(record);

            records.Add(record);
        }

        var existingKeys =
            await GetExistingKeys();

        var newTransactions =
            Deduplicate(records, existingKeys);

        var unknowns =
            ExtractUnknowns(newTransactions);

        if (newTransactions.Any())
        {
            await _db.Transactions
                .AddRangeAsync(newTransactions);
        }

        if (unknowns.Any())
        {
            await _db.UnknownTransactions
                .AddRangeAsync(unknowns);
        }

        await _db.SaveChangesAsync();

        return newTransactions;
    }

    private async Task<HashSet<string>> GetExistingKeys()
    {
        return (await _db.Transactions
                .Select(x => new
                {
                    x.Date,
                    x.Amount,
                    x.Description
                })
                .ToListAsync())
            .Select(x => CreateKey(
                x.Date,
                x.Amount,
                x.Description))
            .ToHashSet();
    }

    private List<BankTransaction> Deduplicate(
        List<BankTransaction> records,
        HashSet<string> existingKeys)
    {
        return records
            .GroupBy(t => CreateKey(
                t.Date,
                t.Amount,
                t.Description))
            .Select(g => g.First())
            .Where(t => !existingKeys.Contains(
                CreateKey(
                    t.Date,
                    t.Amount,
                    t.Description)))
            .ToList();
    }

    private List<UnknownTransaction> ExtractUnknowns(
        List<BankTransaction> transactions)
    {
        return transactions
            .Where(t => t.Category == Category.Other)
            .Select(t => new UnknownTransaction
            {
                Description = t.Description
            })
            .DistinctBy(x => x.Description)
            .ToList();
    }

    private string CreateKey(
        DateTime date,
        decimal amount,
        string description)
    {
        return
            $"{date:yyyyMMdd}-{amount:F2}-{description?.Trim().ToLowerInvariant()}";
    }

    private string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        input = input.ToLowerInvariant();

        input = Regex.Replace(
            input,
            @"\s+",
            " ");

        return input.Trim();
    }
}