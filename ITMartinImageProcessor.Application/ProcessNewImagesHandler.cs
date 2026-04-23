using ITMartinImageProcessor.Application.Interfaces;
using ITMartinImageProcessor.Domain;

namespace ITMartinImageProcessor.Application;

public class ProcessNewImagesHandler
{
    private readonly IImageProcessor _processor;
    private readonly IFileService _fileService;

    public ProcessNewImagesHandler(
        IImageProcessor processor,
        IFileService fileService)
    {
        _processor = processor;
        _fileService = fileService;
    }

    public async Task ExecuteAsync(
        string inputFolder,
        string readyFolder,
        string archiveFolder)
    {
        Console.WriteLine($"Scanning: {inputFolder}");

        var files = _fileService.GetImages(inputFolder).ToList();

        Console.WriteLine($"Found {files.Count} files");

        foreach (var file in files)
        {
            try
            {
                var image = new ImageFile
                {
                    Path = file,
                    CreatedAt = _fileService.GetCreated(file)
                };

                // TEMP: disable until working
                // if (!image.IsRecent())
                //     continue;

                var fileName = Path.GetFileNameWithoutExtension(file);
                var output = Path.Combine(readyFolder, fileName + ".jpg");
                var archive = Path.Combine(archiveFolder, Path.GetFileName(file));

                Console.WriteLine($"Processing: {file}");

                await _processor.ProcessAsync(file, output);

                _fileService.Move(file, archive);

                Console.WriteLine($"Done: {file}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {file} → {ex.Message}");
            }
        }
    }
}