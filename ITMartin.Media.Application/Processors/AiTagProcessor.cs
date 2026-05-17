using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class AiTagProcessor
{
    public void Add(
        MediaFile file,
        IEnumerable<string> tags)
    {
        foreach (var tag in tags)
        {
            if (!file.AiTags.Contains(tag))
            {
                file.AiTags.Add(tag);
            }
        }
    }

    public void Replace(
        MediaFile file,
        IEnumerable<string> tags)
    {
        file.AiTags.Clear();

        foreach (var tag in tags)
        {
            file.AiTags.Add(tag);
        }
    }
}