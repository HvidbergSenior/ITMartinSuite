using System.Globalization;
using CsvHelper.Configuration;

namespace ITMartinBudget.Infrastructure.Csv;

public sealed class TotalkontoRowMap : ClassMap<TotalkontoRow>
{
    public TotalkontoRowMap()
    {
        Map(m => m.Date).Name("Dato").TypeConverterOption.Format("dd.MM.yyyy");
        Map(m => m.Description).Name("Tekst");
        Map(m => m.Amount).Name("Beløb").TypeConverterOption.CultureInfo(new CultureInfo("da-DK"));
        Map(m => m.Balance).Name("Saldo").TypeConverterOption.CultureInfo(new CultureInfo("da-DK"));
        Map(m => m.MainCategory).Name("Hovedkategori");
        Map(m => m.Category).Name("Kategori");
        Map(m => m.Comment).Name("Kommentar");
    }
}
