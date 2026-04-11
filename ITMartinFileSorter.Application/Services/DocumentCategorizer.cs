using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;

namespace ITMartinFileSorter.Application.Services;

public class DocumentCategorizer
{
    public void Categorize(MediaFile file)
    {
        var ext = file.Extension.ToLowerInvariant();

        file.SubCategory = ext switch
        {
            ".pdf" => MediaSubCategory.Pdf,
            ".doc" or ".docx" => MediaSubCategory.Word,
            ".xls" or ".xlsx" => MediaSubCategory.Excel,
            ".txt" => MediaSubCategory.Text,
            ".ppt" or ".pptx" => MediaSubCategory.Presentation,
            ".csv" => MediaSubCategory.Csv,
            _ => MediaSubCategory.UnknownDocument
        };

        // DynamicFolder is always relative and clean
        file.DynamicFolder = Path.Combine("Documents", file.SubCategory.ToString());
    }
}