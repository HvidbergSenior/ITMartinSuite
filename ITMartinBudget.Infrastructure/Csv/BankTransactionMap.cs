using System.Globalization;
using CsvHelper.Configuration;
using ITMartinBudget.Application.Converters;
using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Infrastructure.Csv;

public sealed class BankTransactionMap : ClassMap<BankTransaction>
{
    public BankTransactionMap()
    {
        Map(m => m.Date)
            .Name("Dato")
            .TypeConverterOption.Format("dd.MM.yyyy");

        // 🔥 RAW MUST GO HERE
        Map(m => m.Description)
            .Name("Tekst");

        Map(m => m.Amount)
            .Name("Beløb")
            .TypeConverterOption.CultureInfo(new CultureInfo("da-DK"));

        Map(m => m.SubCategory)
            .Name("Kategori")
            .TypeConverter<SubCategoryConverter>();
    }
}