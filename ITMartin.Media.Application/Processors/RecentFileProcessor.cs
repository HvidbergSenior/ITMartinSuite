using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Processors;

public class RecentFileProcessor
{
    public List<MediaFile> Process(
        IEnumerable<MediaFile> files,
        int days)
    {
        var min =
            DateTime.UtcNow
                .AddDays(-days);

        return files
            .Where(f =>
                f.CreatedAt >= min)
            .ToList();
    }
}