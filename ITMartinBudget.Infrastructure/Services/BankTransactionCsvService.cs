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
            // ✅ Normalize description
            record.Description = record.Description?.Trim().ToLowerInvariant() ?? string.Empty;

            // ✅ Ensure valid enums
            if (!Enum.IsDefined(typeof(MainCategory), record.MainCategory))
                record.MainCategory = MainCategory.Andet;

            if (!Enum.IsDefined(typeof(SubCategory), record.SubCategory))
                record.SubCategory = SubCategory.Ukendt;

            records.Add(record);
        }

        return records;
    }
}