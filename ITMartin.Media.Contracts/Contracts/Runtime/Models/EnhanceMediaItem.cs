using ITMartin.Media.Contracts.Contracts.Runtime.Enums;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class EnhancedMediaItem
{
    public Guid Id
    {
        get;
        init;
    } = Guid.NewGuid();

    public required string OriginalPath
    {
        get;
        init;
    }

    public required string NormalizedPath
    {
        get;
        set;
    }

    public required MediaKind MediaKind
    {
        get;
        init;
    }

    public string? CurrentWorkingPath
    {
        get;
        set;
    }

    public string? AudioWorkingPath
    {
        get;
        set;
    }

    public string? EnhancedOutputPath
    {
        get;
        set;
    }

    public string? ThumbnailOutputPath
    {
        get;
        set;
    }

    public IList<MediaSegment> Segments
    {
        get;
        init;
    } = [];

    public IList<EnhancementOperation> Operations
    {
        get;
        init;
    } = [];

    public IList<string> VideoFilters
    {
        get;
        init;
    } = [];

    public IList<string> AudioFilters
    {
        get;
        init;
    } = [];

    public bool Processing
    {
        get;
        set;
    }

    public bool Failed
    {
        get;
        set;
    }

    public string? FailureReason
    {
        get;
        set;
    }

    public double ProgressPercent
    {
        get;
        set;
    }

    public string? CurrentOperation
    {
        get;
        set;
    }

    public bool IsSample
    {
        get;
        set;
    }

    public bool SkipFurtherProcessing
    {
        get;
        set;
    }

    public TimeSpan? SampleStart
    {
        get;
        set;
    }

    public TimeSpan? SampleDuration
    {
        get;
        set;
    }

    public string? FinalVideoFilterChain
    {
        get;
        set;
    }

    public string? FinalAudioFilterChain
    {
        get;
        set;
    }

    public DateTimeOffset CreatedAt
    {
        get;
        init;
    } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedAt
    {
        get;
        set;
    }

    public DateTimeOffset? CompletedAt
    {
        get;
        set;
    }
}