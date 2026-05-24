namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed record MediaDateResult(
    DateTime? Date,
    bool IsReliable,
    string Source);