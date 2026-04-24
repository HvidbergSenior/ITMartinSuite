using ITMartinImageProcessor.Application.Services;
using ITMartinImageProcessor.Domain.Entities;
using ITMartinImageProcessor.Domain.Interfaces;

namespace ITMartinImageProcessor.Application.Handlers;

public class ProcessNewImagesHandler
{
    private readonly IImageProcessor _processor;
    private readonly IFileService _fileService;
    private readonly HeicToJpgConverterService _converter;

    public ProcessNewImagesHandler(
        IImageProcessor processor,
        IFileService fileService,
        HeicToJpgConverterService converter)
    {
        _processor = processor;
        _fileService = fileService;
        _converter = converter;
    }

    public async Task ExecuteAsync(
        string inputFolder,
        string readyFolder,
        string archiveFolder)
    {
        var files = _fileService.GetImages(inputFolder).ToList();

        foreach (var file in files)
        {
            try
            {
                var image = new ImageFile
                {
                    Path = file,
                    CreatedAt = _fileService.GetCreated(file)
                };

                if (!image.IsRecent())
                    continue;

                // 🔥 CONVERT FIRST
                var converted = await _converter.ConvertAsync(file);

                if (converted == null)
                    continue;

                var fileName = Path.GetFileNameWithoutExtension(converted);

                var output = Path.Combine(readyFolder, fileName + ".jpg");
                var archive = Path.Combine(archiveFolder, Path.GetFileName(file));

                Console.WriteLine($"[PROCESS] {file}");

                await _processor.ProcessAsync(converted, output);

                // 🔥 MOVE ORIGINAL FILE
                _fileService.Move(file, archive);

                // 🔥 OPTIONAL: delete temp JPG if different
                if (converted != file && File.Exists(converted))
                {
                    File.Delete(converted);
                }

                Console.WriteLine($"[DONE] {file}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {file}");
                Console.WriteLine(ex);
            }
        }
    }
}