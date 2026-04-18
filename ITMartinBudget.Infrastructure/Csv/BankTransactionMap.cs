using CsvHelper.Configuration;
using ITMartinBudget.Domain.Entities;
using System.Globalization;

public sealed class BankTransactionMap : ClassMap<BankTransaction>
{
    public BankTransactionMap()
    {
        Map(m => m.Date)
            .Name("Dato")
            .TypeConverterOption.Format("dd.MM.yyyy");

        Map(m => m.Description)
            .Name("Tekst");

        Map(m => m.Amount)
            .Name("Beløb")
            .TypeConverterOption.CultureInfo(new CultureInfo("da-DK"));

        Map(m => m.Category)
            .Name("Kategori"); // 🔥 USE REAL CATEGORY FROM CSV
        Map(m => m.MainCategory).Name("Hovedkategori");
    }
}