using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Categorizers;

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