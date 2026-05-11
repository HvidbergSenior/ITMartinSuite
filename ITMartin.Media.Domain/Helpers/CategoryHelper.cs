using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Enums;

namespace ITMartin.Media.Helpers;

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