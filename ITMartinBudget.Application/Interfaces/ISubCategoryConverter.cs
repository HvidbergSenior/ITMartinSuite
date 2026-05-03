using CsvHelper;

namespace ITMartinBudget.Application.Interfaces;

public interface ISubCategoryConverter
{
    SubCategory Convert(IReaderRow row);
}