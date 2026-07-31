using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Helpers;

public static class CategoryHelper
{
    public static string GetCategory(MediaFile file) =>
        file.SubCategory == MediaSubCategory.Screenshot ? "Screenshots" :
        file.SubCategory == MediaSubCategory.LivePhotoVideo ? "LivePhotos" :
        file.MainCategory switch
        {
            MediaMainCategory.Image => "Images",
            MediaMainCategory.Video => "Videos",
            MediaMainCategory.Document => "Documents",
            MediaMainCategory.Audio => "Audio",
            _ => "Other"
        };
}