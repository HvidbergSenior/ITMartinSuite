using ITMartin.Media.Application.Models;
using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Models;

namespace ITMartin.Media.Application.Pipelines.Package1.Orchestration;

public sealed class Package1WorkflowState
{
    public required string RootPath { get; init; }

    public List<MediaFile>
        MediaFiles { get; set; } = [];

    public List<DuplicateGroup>
        DuplicateGroups { get; set; } = [];

    public Package1CleanupResult?
        CleanupResult { get; set; }

    public Package1Manifest?
        Manifest { get; set; }

    public Package1ExportResult?
        ExportResult { get; set; }
}