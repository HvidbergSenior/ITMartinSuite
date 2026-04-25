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
        var userFolders = Directory.GetDirectories(inputFolder);

        Console.WriteLine("======================================");
        Console.WriteLine($"[SCAN ROOT] {inputFolder}");
        Console.WriteLine($"[USER FOLDERS] {userFolders.Length}");

        foreach (var userFolder in userFolders)
        {
            Console.WriteLine("--------------------------------------");
            Console.WriteLine($"[USER] {userFolder}");

            var files = _fileService.GetImages(userFolder).ToList();

            Console.WriteLine($"[FOUND COUNT] {files.Count}");

            foreach (var file in files)
            {
                try
                {
                    Console.WriteLine("--------------------------------------");
                    Console.WriteLine($"[FOUND FILE] {file}");

                    var fileName = Path.GetFileName(file);

                    // 🔥 skip Synology metadata
                    if (file.Contains("/@eaDir/") || file.Contains("\\@eaDir\\"))
                    {
                        Console.WriteLine("[SKIP] @eaDir");
                        continue;
                    }

                    if (fileName.StartsWith("SYNOPHOTO_"))
                    {
                        Console.WriteLine("[SKIP] Synology generated");
                        continue;
                    }

                    // 🔥 keep folder structure
                    var relativePath = Path.GetRelativePath(inputFolder, file);

                    var outputPath = Path.Combine(
                        readyFolder,
                        Path.ChangeExtension(relativePath, ".jpg"));

                    var archivePath = Path.Combine(archiveFolder, relativePath);

                    Console.WriteLine($"[RELATIVE] {relativePath}");
                    Console.WriteLine($"[OUTPUT] {outputPath}");
                    Console.WriteLine($"[ARCHIVE] {archivePath}");

                    // 🔁 already processed → retry upload only
                    if (File.Exists(outputPath))
                    {
                        Console.WriteLine("[EXISTS] Output exists → retry upload");

                        await _http.UploadAsync(outputPath);
                        continue;
                    }

                    // 🔥 PROCESS
                    try
                    {
                        Console.WriteLine("[PROCESS START]");
                        await _processor.ProcessAsync(file, outputPath);
                        Console.WriteLine("[PROCESS DONE]");
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
                        Console.WriteLine("[UPLOAD START]");
                        await _http.UploadAsync(outputPath);
                        Console.WriteLine("[UPLOAD DONE]");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[UPLOAD ERROR]");
                        Console.WriteLine(ex);
                        continue;
                    }

                    // 🔥 ARCHIVE
                    try
                    {
                        _fileService.Move(file, archivePath);
                        Console.WriteLine("[ARCHIVED]");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[ARCHIVE ERROR]");
                        Console.WriteLine(ex);
                    }

                    Console.WriteLine("[DONE]");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[FATAL ERROR]");
                    Console.WriteLine(ex);
                }
            }
        }
    }
}