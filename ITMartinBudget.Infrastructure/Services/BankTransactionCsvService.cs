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

        var config = new CsvConfiguration(new CultureInfo("da-DK"))
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

        var newUnknowns = new List<UnknownTransaction>();

        foreach (var record in records)
        {
            // 1. Extract MobilePay name
            if (record.IsMobilePay)
            {
                record.MobilePayName = ExtractMobilePayName(record.Description);
                record.MobilePayName = NameNormalizer.NormalizePersonName(record.MobilePayName);
            }

            // 2. 🔥 SINGLE SOURCE OF TRUTH
            var groupingKey = TransactionGroupingService.GetGroupingKey(record);

            // 3. Detection
            if (record.SubCategory == SubCategory.Ukendt)
            {
                var detected = engine.Detect(groupingKey);

                if (detected == SubCategory.Ukendt)
                {
                    if (record.IsMobilePay && DescriptionClassifier.IsLikelyPerson(groupingKey))
                    {
                        detected = SubCategory.Kontooverførsel;
                    }
                }

                record.SubCategory = detected;
            }

            // 4. 💰 INCOME LOGIC (CRITICAL FIX)
            if (record.Amount > 0)
            {
                if (record.SubCategory == SubCategory.Ukendt ||
                    record.SubCategory == SubCategory.Kontooverførsel)
                {
                    record.SubCategory = SubCategory.AndenIndtægt;
                }
            }

            // 5. Map category
            record.Category = CategoryMapper.Map(record.SubCategory);

            // 6. Store normalized value ONLY
            record.Description = groupingKey;

            // 7. Unknown tracking
            if (record.SubCategory == SubCategory.Ukendt)
            {
                newUnknowns.Add(new UnknownTransaction
                {
                    Description = groupingKey
                });
            }
        }

        // 🔥 DEDUP USING NORMALIZED DESCRIPTION
        var uniqueTransactions = records
            .GroupBy(t => CreateKey(t.Date, t.Amount, t.Description))
            .Select(g => g.First())
            .ToList();

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
        if (!description.Contains("mobilepay", StringComparison.OrdinalIgnoreCase))
            return null;

        var cleaned = description
            .Replace("mobilepay", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        cleaned = Regex.Replace(cleaned, @"[^a-zA-ZæøåÆØÅ ]", "");
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

        if (string.IsNullOrWhiteSpace(cleaned))
            return null;

        return CultureInfo.CurrentCulture.TextInfo
            .ToTitleCase(cleaned.ToLower());
    }
}