using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Enums;

namespace ITMartin.Media.Application.Processors;

public class TertiaryCategoryProcessor
{
    public MediaTertiaryCategory Get(
        MediaFile file)
    {
        return file.TertiaryCategory;
    }

    public void Set(
        MediaFile file,
        MediaTertiaryCategory category)
    {
        file.TertiaryCategory =
            category;
    }
}