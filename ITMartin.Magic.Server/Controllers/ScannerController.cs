using Microsoft.AspNetCore.Mvc;

namespace ITMartin.Magic.Server.Controllers;

[ApiController]
[Route("api/scanner")]
[IgnoreAntiforgeryToken]
public class ScannerController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;

    public ScannerController(
        IWebHostEnvironment environment)
    {
        _environment = environment;
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
            Convert.FromBase64String(base64);

        var folder =
            Path.Combine(
                _environment.ContentRootPath,
                "data",
                "frames");

        Directory.CreateDirectory(folder);

        var fileName =
            $"{Guid.NewGuid()}.jpg";

        var path =
            Path.Combine(
                folder,
                fileName);

        Console.WriteLine(
            $"SAVING FRAME: {path}");

        await System.IO.File.WriteAllBytesAsync(
            path,
            bytes);

        return Ok(
            new CaptureFrameResponse
            {
                ImagePath =
                    $"/frames/{fileName}"
            });
    }

    public class CaptureFrameRequest
    {
        public string Base64Image { get; set; } = "";
    }

    public class CaptureFrameResponse
    {
        public string ImagePath { get; set; } = "";
    }
}