namespace ITMartinFileSorter.Application.Services;

public class FastVideoBatchExportService
{
    private readonly FastUniversalVideoConverterService _converter;

    public FastVideoBatchExportService(
        FastUniversalVideoConverterService converter)
    {
        _converter = converter;
    }

    public async Task ConvertAllVideosAsync(
        string exportRoot,
        Action<int, int, string>? progress = null)
    {
        Console.WriteLine("VIDEO BATCH SERVICE FINISHED");
        Console.WriteLine("===== VIDEO CONVERSION DEBUG =====");
        Console.WriteLine($"Export root: {exportRoot}");

        var videoFiles = Directory
            .EnumerateFiles(exportRoot, "*.*", SearchOption.AllDirectories)
            .Where(IsVideoFile)
            .ToList();

        Console.WriteLine($"Video files found: {videoFiles.Count}");

        foreach (var file in videoFiles)
        {
            Console.WriteLine($"FOUND VIDEO: {file}");
        }
        int total = videoFiles.Count;
        int current = 0;

        foreach (var file in videoFiles)
        {
            Console.WriteLine($"[BATCH] Converting: {file}");

            var folder = Path.GetDirectoryName(file)!;

            try
            {
                var output = await _converter
                    .ConvertToUniversalMp4Async(file, folder);

                Console.WriteLine($"[BATCH] Output: {output}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {file}: {ex}");
                continue;
            }

            var done = Interlocked.Increment(ref current);

            progress?.Invoke(
                done,
                total,
                Path.GetFileName(file));
        }

        Console.WriteLine("[BATCH] All conversions done");
    }

    private static bool IsVideoFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        return ext is
            ".avi" or ".mov" or ".mkv" or ".wmv" or
            ".flv" or ".m4v" or ".3gp" or ".mpg" or
            ".mpeg" or ".mp4";
    }
}