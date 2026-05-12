using Microsoft.AspNetCore.Mvc;

namespace ITMartin.Magic.Server.Controllers;

[ApiController]
[Route("api/scanner")]
public class ScannerController : ControllerBase
{
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
                Directory.GetCurrentDirectory(),
                "data",
                "frames");

        Directory.CreateDirectory(folder);

        var fileName =
            $"{Guid.NewGuid()}.jpg";

        var path =
            Path.Combine(
                folder,
                fileName);

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