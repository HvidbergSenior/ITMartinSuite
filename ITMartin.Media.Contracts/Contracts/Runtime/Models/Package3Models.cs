namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public enum Package3IndexType
{
    Faces
}

public sealed class PersonDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public int ReferencePhotoCount { get; init; }
}

public sealed class PersonMatchResult
{
    public required string MediaFilePath { get; init; }
    public double Confidence { get; init; }
    public bool UserConfirmed { get; init; }
}

public sealed class Package3IndexStatus
{
    public required string Status { get; init; } // Running | Completed | Failed
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public string? CurrentFile { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed record ReferencePhotoInput(string FileName, byte[] Bytes);
