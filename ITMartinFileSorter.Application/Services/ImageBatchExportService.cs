namespace ITMartinFileSorter.Application.Services;

public class ImageBatchExportService
{
    private readonly UniversalImageConverterService _converter;

    public ImageBatchExportService(
        UniversalImageConverterService converter)
    {
        _converter = converter;
    }

    public async Task ConvertAllImagesAsync(
        string exportRoot,
        Action<int, int, string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(exportRoot) ||
            !Directory.Exists(exportRoot))
            return;

        var allFiles = Directory
            .EnumerateFiles(exportRoot, "*.*", SearchOption.AllDirectories)
            .ToList();

        var imageFiles = allFiles
            .Where(_converter.NeedsConversion)
            .Where(file => !_converter.ShouldKeepOriginal(file))
            .ToList();

        int total = imageFiles.Count;
        int current = 0;

        foreach (var file in imageFiles)
        {
            try
            {
                Console.WriteLine($"[IMAGE CONVERT] {file}");

                await _converter.ConvertToJpgAsync(file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IMAGE SKIPPED] {file}");
                Console.WriteLine(ex);
            }

            current++;

            progress?.Invoke(
                current,
                total,
                Path.GetFileName(file));
        }

        Console.WriteLine("[IMAGE BATCH] All done");
    }
}