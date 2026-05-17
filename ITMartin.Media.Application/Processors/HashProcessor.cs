using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Interfaces;

namespace ITMartin.Media.Application.Processors;

public class HashProcessor
{
    private readonly IHashService
        _hashService;

    public HashProcessor(
        IHashService hashService)
    {
        _hashService =
            hashService;
    }

    public void Process(
        MediaFile file)
    {
        if (!string.IsNullOrWhiteSpace(
                file.Hash))
        {
            return;
        }

        try
        {
            var hash =
                _hashService
                    .ComputeHash(
                        file.FullPath);

            file.SetHash(hash);
        }
        catch
        {
        }
    }

    public void ProcessBatch(
        IEnumerable<MediaFile> files)
    {
        foreach (var file in files)
        {
            Process(file);
        }
    }
    public Task ProcessAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }
}