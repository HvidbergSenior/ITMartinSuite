using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Enums;
using ITMartin.Media.Interfaces;

namespace ITMartin.Media.Application.Processors;

public class DuplicateProcessor
{
    private readonly IHashService
        _hashService;

    public DuplicateProcessor(
        IHashService hashService)
    {
        _hashService =
            hashService;
    }

    public List<DuplicateGroupResult>
        Process(
            List<MediaFile> files)
    {
        var results =
            new List<DuplicateGroupResult>();

        // ====================================
        // FAST CANDIDATE GROUPS
        // ====================================

        var candidateGroups =
            files
                .GroupBy(f => new
                {
                    f.SizeBytes,
                    f.FileName
                })
                .Where(g =>
                    g.Count() > 1)
                .ToList();

        foreach (var candidate
                 in candidateGroups)
        {
            // ====================================
            // HASH ONLY CANDIDATES
            // ====================================

            foreach (var file
                     in candidate)
            {
                if (string.IsNullOrWhiteSpace(
                        file.Hash))
                {
                    try
                    {
                        file.SetHash(
                            _hashService
                                .ComputeHash(
                                    file.FullPath));
                    }
                    catch
                    {
                    }
                }
            }

            // ====================================
            // REAL DUPLICATES
            // ====================================

            var hashGroups =
                candidate
                    .Where(f =>
                        !string.IsNullOrWhiteSpace(
                            f.Hash))
                    .GroupBy(f =>
                        f.Hash)
                    .Where(g =>
                        g.Count() > 1)
                    .ToList();

            foreach (var hashGroup
                     in hashGroups)
            {
                var groupFiles =
                    hashGroup.ToList();

                var keep =
                    SelectBestFile(
                        groupFiles);

                foreach (var file
                         in groupFiles)
                {
                    file.RequiresReview =
                        false;

                    file.Status =
                        file == keep
                            ? MediaFileStatus.ToKeep
                            : MediaFileStatus.ToDelete;
                }

                results.Add(
                    new DuplicateGroupResult
                    {
                        Keep = keep,
                        Duplicates =
                            groupFiles
                                .Where(f =>
                                    f != keep)
                                .ToList()
                    });
            }
        }

        return results;
    }

    private static MediaFile
        SelectBestFile(
            List<MediaFile> files)
    {
        return files
            .OrderByDescending(f =>
                f.IsDateReliable)

            .ThenByDescending(f =>
                f.CreatedAt ??
                DateTime.MinValue)

            .ThenByDescending(f =>
                f.SizeBytes)

            .ThenBy(f =>
                f.FullPath)

            .First();
    }
}

public class DuplicateGroupResult
{
    public required MediaFile Keep
    {
        get;
        set;
    }

    public List<MediaFile> Duplicates
    {
        get;
        set;
    } = [];
}