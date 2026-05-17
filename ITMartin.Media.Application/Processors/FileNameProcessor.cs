using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileNameProcessor
{
    public string GetName(
        MediaFile file)
    {
        return file.FileName;
    }

    public string GetExtension(
        MediaFile file)
    {
        return file.Extension;
    }

    public string GetNameWithoutExtension(
        MediaFile file)
    {
        return Path.GetFileNameWithoutExtension(
            file.FileName);
    }

    public string Build(
        string name,
        string extension)
    {
        return $"{name}{extension}";
    }
}