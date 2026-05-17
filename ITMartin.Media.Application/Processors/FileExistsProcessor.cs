using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileExistsProcessor
{
    public bool Exists(
        MediaFile file)
    {
        return File.Exists(
            file.FullPath);
    }

    public bool Exists(
        string path)
    {
        return File.Exists(path);
    }
}