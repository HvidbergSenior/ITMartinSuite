namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed record MediaDateRequest(
    string Path,
    int? OverrideYear = null);