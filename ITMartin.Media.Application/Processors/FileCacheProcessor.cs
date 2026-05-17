namespace ITMartin.Media.Application.Processors;

public class FileCacheProcessor<T>
{
    private readonly Dictionary<string, T>
        _cache = [];

    public void Set(
        string key,
        T value)
    {
        _cache[key] =
            value;
    }

    public bool TryGet(
        string key,
        out T? value)
    {
        return _cache
            .TryGetValue(
                key,
                out value);
    }

    public void Clear()
    {
        _cache.Clear();
    }
}