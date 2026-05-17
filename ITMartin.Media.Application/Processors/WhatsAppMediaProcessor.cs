namespace ITMartin.Media.Application.Processors;

using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Interfaces;
using Microsoft.Extensions.Logging;

public sealed class WhatsAppMediaProcessor
{
    private static readonly string[] WhatsAppFolders =
    [
        "WhatsApp Images",
        "WhatsApp Video",
        "WhatsApp Documents",
        "WhatsApp Audio",
        "WhatsApp Animated Gifs",
        "WhatsApp Voice Notes",
        "WhatsApp Stickers"
    ];

    private readonly IFileSystem _fileSystem;
    private readonly ILogger<WhatsAppMediaProcessor> _logger;

    public WhatsAppMediaProcessor(
        IFileSystem fileSystem,
        ILogger<WhatsAppMediaProcessor> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public Task<bool> IsWhatsAppMediaAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedPath = path
            .Replace('\\', '/')
            .ToLowerInvariant();

        var isMatch = WhatsAppFolders.Any(folder =>
            normalizedPath.Contains(
                folder.ToLowerInvariant()));

        return Task.FromResult(isMatch);
    }

    public Task<string?> GetWhatsAppCategoryAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedPath = path
            .Replace('\\', '/')
            .ToLowerInvariant();

        foreach (var folder in WhatsAppFolders)
        {
            if (normalizedPath.Contains(
                    folder.ToLowerInvariant()))
            {
                return Task.FromResult<string?>(folder);
            }
        }

        return Task.FromResult<string?>(null);
    }

    public async Task<IEnumerable<string>> FilterWhatsAppMediaAsync(
        IEnumerable<string> files,
        CancellationToken cancellationToken = default)
    {
        var results = new List<string>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await IsWhatsAppMediaAsync(
                    file,
                    cancellationToken))
            {
                results.Add(file);
            }
        }

        _logger.LogInformation(
            "Found {Count} WhatsApp media files",
            results.Count);

        return results;
    }

    public async Task ProcessAsync(
        IEnumerable<string> files,
        CancellationToken cancellationToken = default)
    {
        var whatsappFiles = await FilterWhatsAppMediaAsync(
            files,
            cancellationToken);

        foreach (var file in whatsappFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var category = await GetWhatsAppCategoryAsync(
                file,
                cancellationToken);

            _logger.LogInformation(
                "WhatsApp media detected: {File} ({Category})",
                file,
                category);
        }
    }
}