namespace ITMartin.Media.Application.Abstractions.Processing;

using ITMartin.Media.Application.Models.Scanning;

public interface IBatchProcessor
{
    Task ProcessAsync(
        ScanBatch batch,
        CancellationToken cancellationToken);
}