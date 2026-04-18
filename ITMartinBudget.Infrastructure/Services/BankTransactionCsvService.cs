using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using ITMartinBudget.Domain.Entities;

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
            // 🔥 CLEAN DATA HERE
            record.Category = record.Category?.Trim().ToLowerInvariant();
            record.Description = record.Description?.Trim();

            records.Add(record);
        }

        return records;
    }
}