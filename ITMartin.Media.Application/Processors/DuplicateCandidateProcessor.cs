using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class DuplicateCandidateProcessor
{
    public List<IGrouping<string, MediaFile>>
        Process(
            IEnumerable<MediaFile> files)
    {
        return files
            .GroupBy(f =>
                $"{f.SizeBytes}_{f.FileName}")
            .Where(g =>
                g.Count() > 1)
            .ToList();
    }
}