namespace ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package1;

public sealed class StartPackage1Request
{
    public required string SourceLibraryPath
    {
        get;
        init;
    }

    public required string WorkingDirectory
    {
        get;
        init;
    }
    public int? OverrideYear
    {
        get;
        init;
    }
    public bool EnableDeduplication
    {
        get;
        init;
    }

    public bool EnableAiClassification
    {
        get;
        init;
    }

    public bool EnableOcr
    {
        get;
        init;
    }

    public required string Profile
    {
        get;
        init;
    }

    public string? OutputPath
    {
        get;
        init;
    }
}