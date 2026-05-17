using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class NormalizationProcessor
{
    public void Process(
        MediaFile file,
        string normalizedPath)
    {
        file.NormalizedPath =
            normalizedPath;
    }

    public void ProcessBatch(
        IEnumerable<MediaFile> files,
        Func<MediaFile, string> pathFactory)
    {
        foreach (var file in files)
        {
            file.NormalizedPath =
                pathFactory(file);
        }
    }
}