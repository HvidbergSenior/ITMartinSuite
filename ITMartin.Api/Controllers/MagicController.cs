using ITMartin.Magic.Application.Interfaces;
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
}