using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileTagProcessor
{
    public void Add(
        MediaFile file,
        string tag)
    {
        if (!file.Tags.Contains(tag))
        {
            file.Tags.Add(tag);
        }
    }

    public void AddRange(
        MediaFile file,
        IEnumerable<string> tags)
    {
        foreach (var tag in tags)
        {
            Add(file, tag);
        }
    }

    public void Remove(
        MediaFile file,
        string tag)
    {
        file.Tags.Remove(tag);
    }

    public bool Has(
        MediaFile file,
        string tag)
    {
        return file.Tags.Contains(tag);
    }
}