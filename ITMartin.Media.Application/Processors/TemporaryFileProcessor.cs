using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class TemporaryFileProcessor
{
    public bool IsTemporary(
        MediaFile file)
    {
        return file.FileName
            .StartsWith(
                "~");

    }

    public List<MediaFile> Process(
        IEnumerable<MediaFile> files)
    {
        return files
            .Where(IsTemporary)
            .ToList();
    }
}