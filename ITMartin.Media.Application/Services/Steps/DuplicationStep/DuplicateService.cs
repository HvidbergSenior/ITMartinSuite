using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Services.Steps.DuplicationStep;

public sealed class DuplicateService
    : IDuplicateService
{
    private readonly ILogger<DuplicateService>
        _logger;

    public DuplicateService(
        ILogger<DuplicateService> logger)
    {
        _logger = logger;
    }

    public async Task<List<DuplicateGroup>> BuildDuplicateGroupsAsync(
        IReadOnlyCollection<MediaFile> files,
        CancellationToken cancellationToken = default)
    {
        var groups = new List<DuplicateGroup>();

        // Pass 1: exact byte-for-byte duplicates - cheap and unambiguous.
        foreach (var byHash in files
                     .Where(x => !string.IsNullOrWhiteSpace(x.Hash))
                     .GroupBy(x => x.Hash)
                     .Where(g => g.Count() > 1))
        {
            var members =
                byHash
                    .OrderByDescending(f => f.SizeBytes)
                    .ToList();

            groups.Add(new DuplicateGroup
            {
                Hash = byHash.Key!,
                Files = members,
                TotalSizeBytes = members.Sum(f => f.SizeBytes)
            });
        }

        // Only identical files are duplicates (user 2026-10-19: "near-duplicates should not be considered duplicate").
        // The old pass 2 (same capture second) and pass 3 (perceptual hash) merged burst shots and the several photos
        // of one Facebook post (same post time) - different pictures the customer wants to keep.

        return groups;
    }
}
