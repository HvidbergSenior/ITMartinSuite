using ITMartinImageProcessor.Domain.Interfaces;
using ITMartinImageProcessor.Application.Services;

namespace ITMartinImageProcessor.Application.Handlers;

public class ProcessImagesHandler
{
    private readonly IFileService _files;
    private readonly IImageProcessor _processor;
    private readonly HttpUploadService _upload;

    public ProcessImagesHandler(
        IFileService files,
        IImageProcessor processor,
        HttpUploadService upload)
    {
        _files = files;
        _processor = processor;
        _upload = upload;
    }

    public async Task ExecuteAsync(string input, string output, string archive)
    {
        Directory.CreateDirectory(input);
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(archive);

        var files = _files.GetImages(input).ToList();

        Console.WriteLine("======================================");
        Console.WriteLine($"[SCAN] {input}");
        Console.WriteLine($"[FOUND COUNT] {files.Count}");

        foreach (var file in files)
        {
            try
            {
                var name = Path.GetFileName(file);
                var outputPath = Path.Combine(output, name);
                var archivePath = Path.Combine(archive, name);

                Console.WriteLine("--------------------------------------");
                Console.WriteLine($"[PROCESS] {file}");

                // avoid partially copied files
                await Task.Delay(500);

                // already processed → retry upload only
                if (File.Exists(outputPath))
                {
                    Console.WriteLine("[EXISTS] Output exists → retry upload + delete source");

                    await _upload.UploadAsync(outputPath);

                    try
                    {
                        _files.Move(file, archivePath);
                    }
                    catch
                    {
                        File.Delete(file); // fallback if move fails
                    }

                    continue;
                }

                // PROCESS
                await _processor.ProcessAsync(file, outputPath);
                Console.WriteLine("[IMAGE DONE]");

                // UPLOAD
                await _upload.UploadAsync(outputPath);
                Console.WriteLine("[UPLOAD DONE]");

                // ARCHIVE
                _files.Move(file, archivePath);
                Console.WriteLine("[ARCHIVED]");

                Console.WriteLine("[DONE]");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {file} → {ex}");
            }
        }
    }
}