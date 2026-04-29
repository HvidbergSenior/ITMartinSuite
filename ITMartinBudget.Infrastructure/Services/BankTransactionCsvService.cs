using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;
using ITMartinBudget.Infrastructure.Csv;
using ITMartinBudget.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBudget.Infrastructure.Services;

public class BankTransactionCsvService
{
    private readonly BudgetDbContext _db;

    public BankTransactionCsvService(BudgetDbContext db)
    {
        _db = db;
    }

    public async Task<List<BankTransaction>> ImportAsync(Stream stream)
    {
        using var reader = new StreamReader(stream);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ";"
        };

        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<BankTransactionMap>();

        var records = new List<BankTransaction>();

        await foreach (var record in csv.GetRecordsAsync<BankTransaction>())
        {
            record.Description = Normalize(record.Description);

            if (record.Description.Length > 200)
                record.Description = record.Description[..200];

            records.Add(record);
        }

        var rules = await _db.CategoryRules.ToListAsync();
        var engine = new CategoryEngine(rules);

        var existingKeys = (await _db.Transactions
            .Select(x => new { x.Date, x.Amount, x.Description })
            .ToListAsync())
            .Select(x => CreateKey(x.Date, x.Amount, x.Description))
            .ToHashSet();

        var existingUnknowns = (await _db.UnknownTransactions
            .Select(x => x.Description)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newUnknowns = new List<UnknownTransaction>();

        foreach (var record in records)
        {
            // 🔥 First: rule engine
            var sub = engine.Detect(record.Description);

            // 🔁 Transfer override (still important)
            if (IsTransfer(record.Description))
            {
                sub = record.Amount >= 0
                    ? SubCategory.OverførselFraAndre
                    : SubCategory.OverførselTilAndre;
            }

            record.SubCategory = Enum.IsDefined(typeof(SubCategory), sub)
                ? sub
                : SubCategory.Ukendt;

            record.Category = CategoryMapper.Map(record.SubCategory);

            // 💰 Fix income sign
            if (record.Category == Category.Indkomst && record.Amount < 0)
                record.Amount = Math.Abs(record.Amount);

            record.MobilePayName = ExtractMobilePayName(record.Description);

            // 🚨 Track unknowns
            if (record.SubCategory == SubCategory.Ukendt &&
                !string.IsNullOrWhiteSpace(record.Description) &&
                !existingUnknowns.Contains(record.Description))
            {
                existingUnknowns.Add(record.Description);

                newUnknowns.Add(new UnknownTransaction
                {
                    Description = record.Description
                });
            }
        }
        // 🔥 DEDUP INSIDE CSV
        var uniqueTransactions = records
            .GroupBy(t => CreateKey(t.Date, t.Amount, t.Description))
            .Select(g => g.First())
            .ToList();

        // 🔥 FILTER AGAINST DB
        var newTransactions = uniqueTransactions
            .Where(t =>
            {
                var key = CreateKey(t.Date, t.Amount, t.Description);
                return !existingKeys.Contains(key);
            })
            .ToList();

        Console.WriteLine($"CSV: {records.Count}");
        Console.WriteLine($"Unique CSV: {uniqueTransactions.Count}");
        Console.WriteLine($"New: {newTransactions.Count}");

        try
        {
            if (newTransactions.Any())
                await _db.Transactions.AddRangeAsync(newTransactions);

            if (newUnknowns.Any())
                await _db.UnknownTransactions.AddRangeAsync(newUnknowns);

            await _db.SaveChangesAsync();
        }
        catch
        {
            Console.WriteLine("⚠ Duplicate detected during insert, ignoring.");
        }

        return newTransactions;
    }

    private bool IsTransfer(string text)
    {
        text = text.ToLowerInvariant();

        return text.Contains("overfør") ||
               text.Contains("egen konto") ||
               text.Contains("konto til konto") ||
               text.Contains("opsparing");
    }

    private string CreateKey(DateTime date, decimal amount, string description)
    {
        var normalizedDescription = (description ?? "")
            .Trim()
            .ToLowerInvariant();

        var normalizedAmount = Math.Round(amount, 2)
            .ToString("F2", CultureInfo.InvariantCulture);

        return $"{date:yyyyMMdd}-{normalizedAmount}-{normalizedDescription}";
    }

    private string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        input = input.ToLowerInvariant();
        input = Regex.Replace(input, @"[^a-zæøå0-9 ]", "");

        return input.Trim();
    }

    private string? ExtractMobilePayName(string description)
    {
        if (!description.Contains("mobilepay"))
            return null;

        var cleaned = description
            .Replace("mobilepay", "")
            .Replace("-", "")
            .Trim();

        if (string.IsNullOrWhiteSpace(cleaned))
            return null;

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleaned);
    }
}