using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;
using ITMartinFileSorter.Domain.Interfaces;

namespace ITMartinFileSorter.Application.Services;

public class DocumentCategorizer : IMediaSubCategorizer
{
    public MediaType Type => MediaType.Document;
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

        
    }
}