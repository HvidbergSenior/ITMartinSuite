using System.Globalization;
using CsvHelper.Configuration;

namespace ITMartinBudget.Infrastructure.Csv;

// Danish bank exports of this style have no header row - columns are
// positional. Distinct from BankTransactionMap, which maps a different,
// header-based export format used for the family budget.
public sealed class RawBankStatementRowMap : ClassMap<RawBankStatementRow>
{
    public RawBankStatementRowMap()
    {
        Map(m => m.Account).Index(0);
        Map(m => m.Account2).Index(1);
        Map(m => m.Account3).Index(2);
        Map(m => m.Date).Index(3).TypeConverterOption.Format("dd-MM-yyyy");
        Map(m => m.Description).Index(4);
        Map(m => m.Amount).Index(5).TypeConverterOption.CultureInfo(new CultureInfo("da-DK"));
        Map(m => m.Balance).Index(6).TypeConverterOption.CultureInfo(new CultureInfo("da-DK"));
        Map(m => m.Info1).Index(7);
        Map(m => m.Info2).Index(8);
        Map(m => m.Note).Index(9);
    }
}
