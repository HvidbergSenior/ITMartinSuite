using Microsoft.AspNetCore.Mvc;

namespace ITMartin.Magic.Server.Controllers;

[ApiController]
[Route("api/scanner")]
[IgnoreAntiforgeryToken]
public class ScannerController : ControllerBase
{
    private readonly IWebHostEnvironment
        _environment;

    public ScannerController(
        IWebHostEnvironment environment)
    {
        _environment =
            environment;
    }

    [HttpPost("frame")]
    public async Task<IActionResult> SaveFrame(
        [FromBody] CaptureFrameRequest request)
    {
        var base64 =
            request.Base64Image
                .Replace(
                    "data:image/jpeg;base64,",
                    "");

        var bytes =
            Convert.FromBase64String(
                base64);

        // =====================================
        // RAW CAPTURE STORAGE
        // =====================================

        var rawFolder =
            Path.Combine(
                _environment.ContentRootPath,
                "data",
                "raw-captures");

        Directory.CreateDirectory(
            rawFolder);

        var fileName =
            $"{Guid.NewGuid()}.jpg";

        var rawPath =
            Path.Combine(
                rawFolder,
                fileName);

        await System.IO.File
            .WriteAllBytesAsync(
                rawPath,
                bytes);

        Console.WriteLine(
            $"RAW FRAME SAVED: {rawPath}");

        // =====================================
        // PIPELINE WORKSPACE
        // =====================================

        var pipelineFolder =
            Path.Combine(
                _environment.ContentRootPath,
                "data",
                "pipeline",
                Path.GetFileNameWithoutExtension(
                    fileName));

        Directory.CreateDirectory(
            pipelineFolder);

        return Ok(
            new CaptureFrameResponse
            {
                ImagePath =
                    rawPath,

                PipelineFolder =
                    pipelineFolder
            });
    }

    public class CaptureFrameRequest
    {
        public string Base64Image { get; set; } =
            "";
    }

    public class CaptureFrameResponse
    {
        public string ImagePath { get; set; } =
            "";

        public string PipelineFolder { get; set; } =
            "";
    }
}