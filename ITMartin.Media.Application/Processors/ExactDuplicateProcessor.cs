using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class ExactDuplicateProcessor
{
    public List<IGrouping<string?, MediaFile>>
        Process(
            IEnumerable<MediaFile> files)
    {
        return files
            .Where(f =>
                !string.IsNullOrWhiteSpace(
                    f.Hash))
            .GroupBy(f =>
                f.Hash)
            .Where(g =>
                g.Count() > 1)
            .ToList();
    }
}