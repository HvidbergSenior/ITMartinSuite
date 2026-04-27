using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;
using ITMartinBudget.Infrastructure.Csv;

namespace ITMartinBudget.Infrastructure.Services;

public class BankTransactionCsvService
{
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
            // Normalize description
            record.Description = record.Description?.Trim().ToLowerInvariant() ?? string.Empty;

            // Ensure enums
            if (!Enum.IsDefined(typeof(MainCategory), record.MainCategory))
                record.MainCategory = MainCategory.Andet;

            if (!Enum.IsDefined(typeof(SubCategory), record.SubCategory))
                record.SubCategory = SubCategory.Ukendt;

            // ✅ FIX income
            if (record.MainCategory == MainCategory.Indkomst && record.Amount < 0)
                record.Amount = Math.Abs(record.Amount);

            // 🔥 MOBILEPAY NAME EXTRACTION
            record.MobilePayName = ExtractMobilePayName(record.Description);

            if (!string.IsNullOrEmpty(record.MobilePayName))
            {
                record.SubCategory = SubCategory.MobilePay;
                record.MainCategory = MainCategory.Andet;
            }

            records.Add(record);
        }

        return records;
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