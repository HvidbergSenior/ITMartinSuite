using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace ITMartinFileSorter.Server.Controllers;

[ApiController]
[Route("api/media")]
public class MediaController : ControllerBase
{
    private readonly IConfiguration _config;

    public MediaController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet]
    public IActionResult Get([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return NotFound();

        var libraryRoot = _config["MediaSettings:LibraryRoot"] ?? "";
        var sourceRoot  = _config["MediaSettings:SourceRoot"]  ?? "";

        if (!PathSecurity.IsUnder(path, libraryRoot) && !PathSecurity.IsUnder(path, sourceRoot))
            return NotFound();

        if (!System.IO.File.Exists(path))
            return NotFound();

        var provider = new FileExtensionContentTypeProvider();

        if (!provider.TryGetContentType(path, out var contentType))
            contentType = "application/octet-stream";

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read);

        return File(stream, contentType);
    }
}
