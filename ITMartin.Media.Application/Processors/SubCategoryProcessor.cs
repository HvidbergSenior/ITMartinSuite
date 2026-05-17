using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Enums;

namespace ITMartin.Media.Application.Processors;

public class SubCategoryProcessor
{
    public MediaSubCategory Get(
        MediaFile file)
    {
        return file.SubCategory;
    }

    public void Set(
        MediaFile file,
        MediaSubCategory category)
    {
        file.SubCategory =
            category;
    }

    public bool IsScreenshot(
        MediaFile file)
    {
        return file.SubCategory ==
               MediaSubCategory.Screenshot;
    }

    public bool IsPhonePhoto(
        MediaFile file)
    {
        return file.SubCategory ==
               MediaSubCategory.PhonePhoto;
    }

    public bool IsPhoneVideo(
        MediaFile file)
    {
        return file.SubCategory ==
               MediaSubCategory.PhoneVideo;
    }
}