using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Processors;
using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Models;

namespace ITMartin.Media.Application.Services;

public class DuplicateService : IDuplicateService
{
    private readonly DuplicateProcessor
        _duplicateProcessor;

    public DuplicateService(
        DuplicateProcessor
            duplicateProcessor)
    {
        _duplicateProcessor =
            duplicateProcessor;
    }

    public string FolderPath
    {
        get;
        set;
    } = "";

    public List<MediaFile> AllFiles
    {
        get;
        set;
    } = [];

    public List<AiCollection> AiCollections
    {
        get;
        set;
    } = [];

    public List<DuplicateGroup> DuplicateGroups
    {
        get;
        set;
    } = [];

    public void Reset()
    {
        AllFiles.Clear();

        AiCollections.Clear();

        DuplicateGroups.Clear();
    }

    public void BuildDuplicateGroups()
    {
        DuplicateGroups.Clear();

        var results =
            _duplicateProcessor
                .Process(AllFiles);

        foreach (var result in results)
        {
            var files =
                new List<DuplicateFile>();

            files.Add(
                new DuplicateFile
                {
                    Path = result.Keep.FullPath,
                    FileName = result.Keep.FileName,
                    SizeBytes = result.Keep.SizeBytes,
                    CreatedAt = result.Keep.CreatedAt ??
                                DateTime.MinValue,
                    ModifiedAt = result.Keep.CreatedAt ??
                                 DateTime.MinValue,
                    IsOriginal = true
                });

            files.AddRange(
                result.Duplicates.Select(
                    x => new DuplicateFile
                    {
                        Path = x.FullPath,
                        FileName = x.FileName,
                        SizeBytes = x.SizeBytes,
                        CreatedAt = x.CreatedAt ??
                                    DateTime.MinValue,
                        ModifiedAt = x.CreatedAt ??
                                     DateTime.MinValue,
                        IsOriginal = false
                    }));

            DuplicateGroups.Add(
                new DuplicateGroup
                {
                    Hash = result.Keep.Hash ?? "",
                    TotalSizeBytes =
                        files.Sum(x => x.SizeBytes),
                    Files = files
                });
        }
    }
}