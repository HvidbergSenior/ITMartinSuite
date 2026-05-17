using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class ConversionCandidateProcessor
{
    public List<MediaFile> Process(
        IEnumerable<MediaFile> files)
    {
        return files
            .Where(f =>
                string.IsNullOrWhiteSpace(
                    f.NormalizedPath))
            .ToList();
    }
}