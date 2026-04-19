using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Converters;

public class MainCategoryConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        var kategori = text?.Trim().ToLowerInvariant() ?? "";

        if (kategori.Contains("løn") || kategori.Contains("indtægt"))
            return MainCategory.Indkomst;

        if (kategori.Contains("dagligvarer") || kategori.Contains("restaurant"))
            return MainCategory.Mad;

        if (kategori.Contains("brændstof") || kategori.Contains("parkering"))
            return MainCategory.Transport;

        if (kategori.Contains("realkredit") || kategori.Contains("bolig"))
            return MainCategory.Bolig;

        if (kategori.Contains("telefon") || kategori.Contains("streaming"))
            return MainCategory.Abonnement;

        if (kategori.Contains("film") || kategori.Contains("sport"))
            return MainCategory.Fritid;

        if (kategori.Contains("tøj"))
            return MainCategory.Shopping;

        if (kategori.Contains("læge") || kategori.Contains("medicin"))
            return MainCategory.Sundhed;

        return MainCategory.Andet;
    }
}