using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileGroupingProcessor
{
    public Dictionary<int, List<MediaFile>>
        GroupByYear(
            IEnumerable<MediaFile> files)
    {
        return files
            .GroupBy(f =>
                f.Year)
            .ToDictionary(
                g => g.Key,
                g => g.ToList());
    }

    public Dictionary<int, List<MediaFile>>
        GroupByMonth(
            IEnumerable<MediaFile> files)
    {
        return files
            .GroupBy(f =>
                f.Month)
            .ToDictionary(
                g => g.Key,
                g => g.ToList());
    }
}