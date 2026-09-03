using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Services.Steps.DuplicationStep;

public sealed class DuplicateService
    : IDuplicateService
{
    // Below this many differing bits (of 64), two images are treated as the
    // same photo re-saved at a different quality/compression rather than two
    // genuinely different photos. 6 bits (~90% match) is conservative enough
    // to avoid merging two distinct-but-similar photos from the same event.
    private const int NearDuplicateHammingThreshold = 6;

    private readonly IPerceptualHashService
        _perceptualHashService;

    private readonly ILogger<DuplicateService>
        _logger;

    public DuplicateService(
        IPerceptualHashService perceptualHashService,
        ILogger<DuplicateService> logger)
    {
        _perceptualHashService = perceptualHashService;
        _logger = logger;
    }

    public async Task<List<DuplicateGroup>> BuildDuplicateGroupsAsync(
        IReadOnlyCollection<MediaFile> files,
        CancellationToken cancellationToken = default)
    {
        var groups = new List<DuplicateGroup>();
        var grouped = new HashSet<MediaFile>();

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

            foreach (var file in members)
                grouped.Add(file);
        }

        // Pass 2: exact reliable-date match. A file's own already-resolved
        // capture timestamp (real EXIF/video/document metadata, full
        // second precision, set earlier by MetadataWorkflowStep) is a
        // strong, free duplicate signal on its own - two files sharing the
        // exact same capture instant are essentially always the same shot
        // saved twice. Deliberately full DateTime equality, not just the
        // same day/minute: same-*minute* matching was tried and produced
        // massive false positives against ordinary burst photography
        // (consecutive shots a few seconds apart landing in the same
        // minute). Only ever considers dates MediaDateService itself
        // marked reliable (real metadata, not a filesystem-timestamp
        // fallback), and excludes today outright as an extra guard against
        // a copy-date slipping through as "reliable".
        var today = DateTime.Today;

        foreach (var byDate in files
                     .Where(f => !grouped.Contains(f) && f.IsDateReliable && f.CreatedAt.HasValue && f.CreatedAt.Value.Date != today)
                     .GroupBy(f => f.CreatedAt!.Value)
                     .Where(g => g.Count() > 1))
        {
            var members =
                byDate
                    .OrderByDescending(f => f.SizeBytes)
                    .ToList();

            groups.Add(new DuplicateGroup
            {
                Hash = $"datematch:{byDate.Key:O}",
                Files = members,
                TotalSizeBytes = members.Sum(f => f.SizeBytes)
            });

            foreach (var file in members)
                grouped.Add(file);
        }

        // Pass 3: near-duplicates. A photo re-imported from a second source
        // (e.g. an iCloud recovery batch, a second phone backup) gets
        // recompressed along the way, so its bytes - and therefore its exact
        // hash - differ from the original even though it's visually the same
        // picture. Exact-hash grouping above misses these entirely, which is
        // exactly how thousands of visual duplicates built up undetected in
        // a real tenant library. Catch those by decoded pixel content
        // instead of bytes.
        //
        // Scoped to images only (video near-dup detection would need frame
        // extraction per candidate, not worth the cost here) and bucketed by
        // capture Year/Month so we only ever compare photos that could
        // plausibly be the same shot - both keeps this fast and guards
        // against merging two different but visually-similar photos from
        // unrelated dates.
        var nearDuplicateGroups = 0;
        var hashDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount);
        var hashParallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = hashDegreeOfParallelism,
            CancellationToken = cancellationToken
        };

        foreach (var bucket in files
                     .Where(f => f.IsImage && !grouped.Contains(f))
                     .GroupBy(f => (f.Year, f.Month)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Perceptual hashing decodes + resizes every image, so this was
            // by far the slowest part of the whole QuickSort pipeline running
            // one file at a time (9+ hours on a 34K-file library). Hashes
            // are computed in parallel into an index-aligned array so the
            // O(n^2) grouping loop below still sees the exact same file
            // order as the old sequential version - only the hashing itself
            // is parallelized, not the grouping semantics.
            var bucketFiles = bucket.ToList();
            var hashes = new ulong?[bucketFiles.Count];

            await Parallel.ForEachAsync(
                Enumerable.Range(0, bucketFiles.Count),
                hashParallelOptions,
                async (i, ct) =>
                {
                    hashes[i] = await _perceptualHashService.ComputeAsync(
                        bucketFiles[i].FullPath,
                        ct);
                });

            var hashed = new List<(MediaFile File, ulong Hash)>();

            for (var i = 0; i < bucketFiles.Count; i++)
            {
                if (hashes[i] is { } h)
                {
                    hashed.Add((bucketFiles[i], h));
                }
            }

            var used = new bool[hashed.Count];

            for (var i = 0; i < hashed.Count; i++)
            {
                if (used[i]) continue;

                var members = new List<MediaFile> { hashed[i].File };

                for (var j = i + 1; j < hashed.Count; j++)
                {
                    if (used[j]) continue;

                    if (_perceptualHashService.HammingDistance(hashed[i].Hash, hashed[j].Hash) <= NearDuplicateHammingThreshold)
                    {
                        members.Add(hashed[j].File);
                        used[j] = true;
                    }
                }

                if (members.Count > 1)
                {
                    used[i] = true;
                    nearDuplicateGroups++;

                    var ordered =
                        members
                            .OrderByDescending(f => f.SizeBytes)
                            .ToList();

                    groups.Add(new DuplicateGroup
                    {
                        Hash = $"phash:{hashed[i].Hash:x16}",
                        Files = ordered,
                        TotalSizeBytes = ordered.Sum(f => f.SizeBytes)
                    });
                }
            }
        }

        if (nearDuplicateGroups > 0)
        {
            _logger.LogInformation(
                "Perceptual-hash pass found {Groups} near-duplicate image groups (recompressed re-imports etc.)",
                nearDuplicateGroups);
        }

        return groups;
    }
}
