using ITMartinImageProcessor.Application.Services;
using ITMartinImageProcessor.Domain.Interfaces;

namespace ITMartinImageProcessor.Application.Handlers;

public class ProcessNewImagesHandler
{
    private readonly IImageProcessor _processor;
    private readonly IFileService _fileService;
    private readonly HttpUploadService _http;

    public ProcessNewImagesHandler(
        IImageProcessor processor,
        IFileService fileService,
        HttpUploadService http)
    {
        _processor = processor;
        _fileService = fileService;
        _http = http;
    }

    public async Task ExecuteAsync(
        string inputFolder,
        string readyFolder,
        string archiveFolder)
    {
        var files = Directory
            .EnumerateFiles(inputFolder, "*.*", SearchOption.AllDirectories)
            .ToList();

        Console.WriteLine("======================================");
        Console.WriteLine($"[SCAN] {inputFolder}");
        Console.WriteLine($"[FOUND COUNT] {files.Count}");

        foreach (var file in files)
        {
            try
            {
                Console.WriteLine("--------------------------------------");
                Console.WriteLine($"[FOUND FILE] {file}");

                var fileName = Path.GetFileName(file);
                var ext = Path.GetExtension(file).ToLowerInvariant();

                Console.WriteLine($"[FILENAME] {fileName}");
                Console.WriteLine($"[EXT] {ext}");

                // 🔥 ignore Synology metadata folders
                if (file.Contains("/@eaDir/") || file.Contains("\\@eaDir\\"))
                {
                    Console.WriteLine("[SKIP] @eaDir");
                    continue;
                }

                // 🔥 ignore Synology generated files
                if (fileName.StartsWith("SYNOPHOTO_"))
                {
                    Console.WriteLine("[SKIP] Synology generated");
                    continue;
                }

                // 🔥 extension filter
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
                {
                    Console.WriteLine("[SKIP] Not image");
                    continue;
                }

                var outputFileName = Path.GetFileNameWithoutExtension(file) + ".jpg";
                var outputPath = Path.Combine(readyFolder, outputFileName);

                Console.WriteLine($"[OUTPUT PATH] {outputPath}");

                if (File.Exists(outputPath))
                {
                    Console.WriteLine("[EXISTS] Output already exists → retry upload");

                    await _http.UploadAsync(outputPath);

                    continue;
                }

                // 🔥 PROCESS
                try
                {
                    Console.WriteLine($"[PROCESS START] {file}");
                    await _processor.ProcessAsync(file, outputPath);
                    Console.WriteLine($"[PROCESS DONE] {outputPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[PROCESS ERROR]");
                    Console.WriteLine(ex);
                    continue;
                }

                // 🔥 UPLOAD
                try
                {
                    Console.WriteLine($"[UPLOAD START] {outputPath}");
                    await _http.UploadAsync(outputPath);
                    Console.WriteLine($"[UPLOAD DONE]");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[UPLOAD ERROR]");
                    Console.WriteLine(ex);
                    continue;
                }

                // 🔥 ARCHIVE
                var archivePath = Path.Combine(archiveFolder, fileName);

                try
                {
                    _fileService.Move(file, archivePath);
                    Console.WriteLine($"[ARCHIVE] {archivePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[ARCHIVE ERROR]");
                    Console.WriteLine(ex);
                }

                Console.WriteLine($"[DONE] {file}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FATAL ERROR] {file}");
                Console.WriteLine(ex);
            }
        }
    }
}