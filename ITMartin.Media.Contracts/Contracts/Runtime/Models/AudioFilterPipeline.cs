namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class AudioFilterPipeline
{
    private readonly List<string>
        _filters = [];

    public bool HasFilters =>
        _filters.Count > 0;

    public void Add(
        string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return;
        }

        _filters.Add(filter);
    }

    public string Build()
    {
        return string.Join(
            ",",
            _filters);
    }

    public IReadOnlyCollection<string> Filters =>
        _filters.AsReadOnly();
}