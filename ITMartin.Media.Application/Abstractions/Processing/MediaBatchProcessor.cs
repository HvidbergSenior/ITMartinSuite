namespace ITMartin.Media.Infrastructure.Processing;

using ITMartin.Media.Application.Abstractions.Processing;
using ITMartin.Media.Application.Models.Scanning;
using Microsoft.Extensions.Logging;

public sealed class MediaBatchProcessor
    : IBatchProcessor
{
    private readonly ILogger<MediaBatchProcessor>
        _logger;

    public MediaBatchProcessor(
        ILogger<MediaBatchProcessor> logger)
    {
        _logger = logger;
    }

    public async Task ProcessAsync(
        ScanBatch batch,
        CancellationToken cancellationToken)
    {
        foreach (var file in batch.Files)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            _logger.LogInformation(
                "Processing batch file {File}",
                file);

            await Task.Delay(
                10,
                cancellationToken);
        }
    }
}