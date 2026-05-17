using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class MemeProcessor
{
    public List<MediaFile> Process(
        IEnumerable<MediaFile> files)
    {
        return files
            .Where(f =>
                f.Tags.Contains("meme")
                ||
                f.AiTags.Contains("meme"))
            .ToList();
    }
}