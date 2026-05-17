using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Enums;

namespace ITMartin.Media.Application.Processors;

public class FileStatusProcessor
{
    public void MarkKeep(
        MediaFile file)
    {
        file.Status =
            MediaFileStatus.ToKeep;

        file.RequiresReview =
            false;
    }

    public void MarkDelete(
        MediaFile file)
    {
        file.Status =
            MediaFileStatus.ToDelete;

        file.RequiresReview =
            false;
    }

    public void MarkReview(
        MediaFile file)
    {
        file.Status =
            MediaFileStatus.Initial;

        file.RequiresReview =
            true;
    }

    public bool IsKeep(
        MediaFile file)
    {
        return file.Status ==
               MediaFileStatus.ToKeep;
    }

    public bool IsDelete(
        MediaFile file)
    {
        return file.Status ==
               MediaFileStatus.ToDelete;
    }

    public bool RequiresReview(
        MediaFile file)
    {
        return file.RequiresReview;
    }
}