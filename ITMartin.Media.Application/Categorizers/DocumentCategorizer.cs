using ITMartin.Media.Entities;
using ITMartin.Media.Enums;
using ITMartin.Media.Interfaces;

namespace ITMartin.Media.Categorizers;

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