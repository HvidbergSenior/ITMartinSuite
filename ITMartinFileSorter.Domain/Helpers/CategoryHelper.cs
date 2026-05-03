using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;

namespace ITMartinFileSorter.Domain.Helpers;

public static class CategoryHelper
{
    public static string GetCategory(MediaFile file) =>
        file.SubCategory == MediaSubCategory.Screenshot ? "Screenshots" :
        file.SubCategory == MediaSubCategory.Meme ? "Memes" :
        file.MainCategory switch
        {
            MediaMainCategory.Image => "Images",
            MediaMainCategory.Video => "Videos",
            MediaMainCategory.Document => "Documents",
            MediaMainCategory.Audio => "Audio",
            _ => "Other"
        };
}