using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

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