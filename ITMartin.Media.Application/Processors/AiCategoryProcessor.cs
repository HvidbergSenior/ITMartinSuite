using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class AiCategoryProcessor
{
    public void Process(
        MediaFile file,
        string? category,
        string? subCategory = null)
    {
        file.AiCategory =
            category;

        file.AiSubCategory =
            subCategory;

        file.AiProcessed =
            true;
    }
}