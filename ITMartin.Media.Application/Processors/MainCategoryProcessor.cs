using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Processors;

public class MainCategoryProcessor
{
    public MediaMainCategory Get(
        MediaFile file)
    {
        return file.MainCategory;
    }

    public bool IsImage(
        MediaFile file)
    {
        return file.MainCategory ==
               MediaMainCategory.Image;
    }

    public bool IsVideo(
        MediaFile file)
    {
        return file.MainCategory ==
               MediaMainCategory.Video;
    }

    public bool IsAudio(
        MediaFile file)
    {
        return file.MainCategory ==
               MediaMainCategory.Audio;
    }

    public bool IsDocument(
        MediaFile file)
    {
        return file.MainCategory ==
               MediaMainCategory.Document;
    }
}