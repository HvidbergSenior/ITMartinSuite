using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace ITMartinBudget.Infrastructure.Csv;

// Hvidberg's and ITMartin's bank export shape: headered, semicolon-delimited,
// UTF-8 with BOM, and - unlike Bogshoppen's raw export - the bank already
// assigns each row a category (Hovedkategori/Kategori), which becomes the
// row's SuggestedCategoryName so LedgerImportService can seed a CategoryRule
// automatically instead of leaving every row for manual categorization.
public sealed class TotalkontoParser : IBankStatementParser
{
    public bool CanParse(string firstLine) =>
        firstLine.TrimStart('﻿').StartsWith("Dato;Tekst;", StringComparison.OrdinalIgnoreCase);

    public async Task<List<NormalizedImportRow>> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);

        var config = new CsvConfiguration(new CultureInfo("da-DK"))
        {
            Delimiter = ";",
            MissingFieldFound = null,
            BadDataFound = null,
        };

        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<TotalkontoRowMap>();

        var rows = new List<NormalizedImportRow>();
        await foreach (var row in csv.GetRecordsAsync<TotalkontoRow>(cancellationToken))
        {
            var rawDetails = row.MainCategory;
            // "Andet"/"Andet (Overførsel)" is the bank's own "couldn't
            // categorize this either" bucket - no better than leaving it
            // unset, so don't seed a rule from it.
            var suggested = string.IsNullOrWhiteSpace(row.Category) || row.Category.StartsWith("Andet", StringComparison.OrdinalIgnoreCase)
                ? null
                : row.Category;

            rows.Add(new NormalizedImportRow(row.Date, row.Description.Trim(), row.Amount, row.Balance, rawDetails, suggested));
        }

        return rows;
    }
}
