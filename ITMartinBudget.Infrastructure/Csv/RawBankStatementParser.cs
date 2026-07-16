using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace ITMartinBudget.Infrastructure.Csv;

// Bogshoppen's bank export shape: no header, semicolon-delimited, positional
// columns, Windows-1252 - the format a bank hands you directly, not a
// spreadsheet export. No bank-side categorization exists in this shape
// (unlike TotalkontoParser), so SuggestedCategoryName is always null here.
public sealed class RawBankStatementParser : IBankStatementParser
{
    static RawBankStatementParser()
    {
        // .NET Core doesn't ship legacy code pages (like Windows-1252) by
        // default - has to be registered once before Encoding.GetEncoding(1252)
        // will resolve instead of throwing.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    // The fallback parser - matches anything that isn't a recognized headered
    // shape, since this format has no header/signature of its own to detect.
    public bool CanParse(string firstLine) => true;

    public async Task<List<NormalizedImportRow>> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.GetEncoding(1252));

        var config = new CsvConfiguration(new CultureInfo("da-DK"))
        {
            Delimiter = ";",
            HasHeaderRecord = false,
            MissingFieldFound = null,
            BadDataFound = null,
        };

        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<RawBankStatementRowMap>();

        var rows = new List<NormalizedImportRow>();
        await foreach (var row in csv.GetRecordsAsync<RawBankStatementRow>(cancellationToken))
        {
            var description = row.Description.Trim();
            var rawDetails = string.Join(" ", new[] { row.Info1, row.Info2, row.Note }
                .Select(s => s.Trim())
                .Where(s => s.Length > 0));

            rows.Add(new NormalizedImportRow(row.Date, description, row.Amount, row.Balance, rawDetails, null));
        }

        return rows;
    }
}
