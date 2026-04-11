namespace ITMartinFileSorter.Application.Services;

public class VideoDisplayFilterService
{
    public IEnumerable<string> FilterConvertedVideos(IEnumerable<string> files)
    {
        var list = files.ToList();

        var fixedNames = list
            .Where(f => Path.GetFileNameWithoutExtension(f)
                .EndsWith("_fixed", StringComparison.OrdinalIgnoreCase))
            .Select(f =>
            {
                var name = Path.GetFileNameWithoutExtension(f);
                return name[..^6]; // remove "_fixed"
            })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return list.Where(file =>
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();

            if (ext != ".mp4")
            {
                var originalName = Path.GetFileNameWithoutExtension(file);

                if (fixedNames.Contains(originalName))
                    return false;
            }

            return true;
        });
    }
}