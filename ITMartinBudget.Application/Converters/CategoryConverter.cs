using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using ITMartinBudget.Domain.Enums;
using ITMartinBudget.Application.Services;

namespace ITMartinBudget.Application.Converters;

public class CategoryConverter : DefaultTypeConverter
{
    private static readonly SubCategoryConverter _subConverter = new();

    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        var sub = (SubCategory)_subConverter.ConvertFromString(text, row, memberMapData);

        return CategoryMapper.Map(sub);
    }
}