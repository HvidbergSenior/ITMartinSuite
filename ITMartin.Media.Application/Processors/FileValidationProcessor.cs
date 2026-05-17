using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileValidationProcessor
{
    public bool Exists(
        MediaFile file)
    {
        return File.Exists(
            file.FullPath);
    }

    public bool HasPath(
        MediaFile file)
    {
        return !string.IsNullOrWhiteSpace(
            file.FullPath);
    }

    public bool HasDate(
        MediaFile file)
    {
        return file.CreatedAt != null;
    }
}