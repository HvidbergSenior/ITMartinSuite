namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed record MediaDateResult(
    DateTime? Date,
    bool IsReliable,
    string Source,
    // True when Date's year came from an ancestor folder name (e.g. a
    // "2010" folder) rather than real metadata or a full parsed date - the
    // year is trustworthy enough to sort by, but Month/Day are placeholders
    // (always January 1st) and must never be presented as a real date.
    bool IsYearOnly = false);