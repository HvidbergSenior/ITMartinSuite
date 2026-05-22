using ITMartin.Media.Application.Pipelines.Package1.Models.Scanning;

namespace ITMartin.Media.Application.Services;

public sealed class ScanBatchingService
{
    public IReadOnlyCollection<ScanBatch>
        CreateBatches(
            Guid sessionId,
            IReadOnlyCollection<string> files,
            int batchSize)
    {
        var batches =
            new List<ScanBatch>();

        var batchNumber = 0;

        foreach (var chunk in files.Chunk(batchSize))
        {
            batches.Add(
                new ScanBatch
                {
                    Id = Guid.NewGuid(),

                    SessionId = sessionId,

                    Files = chunk.ToList(),

                    BatchNumber = ++batchNumber,

                    CreatedAt =
                        DateTimeOffset.UtcNow
                });
        }

        return batches;
    }
}