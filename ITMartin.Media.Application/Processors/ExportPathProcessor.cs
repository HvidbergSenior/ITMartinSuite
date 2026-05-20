using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Processors;

public class ExportPathProcessor
{
    public void Process(
        MediaFile file,
        string exportPath)
    {
        file.ExportedPath =
            exportPath;
    }

    public void ProcessBatch(
        IEnumerable<MediaFile> files,
        Func<MediaFile, string> pathFactory)
    {
        foreach (var file in files)
        {
            file.ExportedPath =
                pathFactory(file);
        }
    }
}