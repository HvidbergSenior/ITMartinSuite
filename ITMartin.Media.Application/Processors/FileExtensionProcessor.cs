using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileExtensionProcessor
{
    public string Get(
        MediaFile file)
    {
        return file.Extension;
    }

    public List<MediaFile> Filter(
        IEnumerable<MediaFile> files,
        params string[] extensions)
    {
        return files
            .Where(f =>
                extensions.Contains(
                    f.Extension,
                    StringComparer
                        .OrdinalIgnoreCase))
            .ToList();
    }
}