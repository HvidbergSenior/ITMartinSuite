using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class PotentialDuplicateProcessor
{
    public List<IGrouping<long, MediaFile>>
        Process(
            IEnumerable<MediaFile> files)
    {
        return files
            .GroupBy(f =>
                f.SizeBytes)
            .Where(g =>
                g.Count() > 1)
            .ToList();
    }
}