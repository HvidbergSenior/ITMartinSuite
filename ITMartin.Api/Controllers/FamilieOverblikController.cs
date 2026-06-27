using ITMartin.FamilieOverblik.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace ITMartin.Api.Controllers;

[ApiController]
[Route("api/familie")]
public sealed class FamilieOverblikController : ControllerBase
{
    private readonly FamilyTaskService _service;
    private readonly IConfiguration _config;

    public FamilieOverblikController(
        FamilyTaskService service,
        IConfiguration config)
    {
        _service = service;
        _config = config;
    }

    [HttpGet("today")]
    public async Task<IActionResult> GetToday(CancellationToken ct)
    {
        var tasks = await _service.GetTodayAsync(ct);
        return Ok(tasks);
    }

    [HttpPost("tasks")]
    public async Task<IActionResult> Create(
        [FromForm] string type,
        [FromForm] string createdBy,
        [FromForm] string? note,
        IFormFile? photo,
        CancellationToken ct)
    {
        string? photoPath = null;

        if (photo is not null)
        {
            var photosRoot = _config["FamilieOverblik:PhotosRoot"]
                ?? Path.Combine(Path.GetTempPath(), "familie-photos");

            Directory.CreateDirectory(photosRoot);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(photo.FileName)}";
            var filePath = Path.Combine(photosRoot, fileName);

            await using var stream = System.IO.File.Create(filePath);
            await photo.CopyToAsync(stream, ct);

            photoPath = fileName;
        }

        var task = await _service.CreateAsync(type, note, photoPath, createdBy, ct);
        return Ok(task);
    }

    [HttpPut("tasks/{id:guid}/claim")]
    public async Task<IActionResult> Claim(
        Guid id,
        [FromBody] ClaimRequest request,
        CancellationToken ct)
    {
        var ok = await _service.ClaimAsync(id, request.ClaimedBy, ct);
        return ok ? Ok() : NotFound();
    }

    [HttpPut("tasks/{id:guid}/complete")]
    public async Task<IActionResult> Complete(
        Guid id,
        CancellationToken ct)
    {
        var ok = await _service.CompleteAsync(id, ct);
        return ok ? Ok() : NotFound();
    }

    [HttpGet("photos/{fileName}")]
    public IActionResult GetPhoto(string fileName)
    {
        var photosRoot = _config["FamilieOverblik:PhotosRoot"]
            ?? Path.Combine(Path.GetTempPath(), "familie-photos");

        var filePath = Path.Combine(photosRoot, fileName);

        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".heic" => "image/heic",
            _ => "application/octet-stream"
        };

        return PhysicalFile(filePath, contentType);
    }
}

public record ClaimRequest(string ClaimedBy);
