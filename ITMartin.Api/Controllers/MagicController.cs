using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace ITMartin.Api.Controllers;

[ApiController]
[Route("api/magic")]
public sealed class MagicController
    : ControllerBase
{
    private readonly ICardScanOrchestrator _orchestrator;

    public MagicController(
        ICardScanOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [HttpPost("scan")]
    public async Task<IActionResult> Scan(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}");

        await using var stream =
            System.IO.File.Create(path);

        await file.CopyToAsync(
            stream,
            cancellationToken);

        var result =
            await _orchestrator.ExecuteAsync(
                path,
                cancellationToken);

        return Ok(result);
    }
    [HttpPost("scan-capture")]
    public async Task<IActionResult> ScanCapture(
        [FromBody] ScanCaptureRequest request,
        CancellationToken cancellationToken)
    {
        var base64 =
            request.Image;

        var comma =
            base64.IndexOf(',');

        if (comma >= 0)
        {
            base64 =
                base64[(comma + 1)..];
        }

        var bytes =
            Convert.FromBase64String(base64);

        var path =
            Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid()}.jpg");

        await System.IO.File.WriteAllBytesAsync(
            path,
            bytes,
            cancellationToken);

        var result =
            await _orchestrator.ExecuteAsync(
                path,
                cancellationToken);

        return Ok(result);
    }
}