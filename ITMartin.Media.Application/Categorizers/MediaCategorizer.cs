using ITMartin.Media.Entities;
using ITMartin.Media.Enums;
using ITMartin.Media.Interfaces;

namespace ITMartin.Media.Categorizers;

public class MediaCategorizer
{
    private readonly Dictionary<MediaType, IMediaSubCategorizer> _map;

    public MediaCategorizer(IEnumerable<IMediaSubCategorizer> categorizers)
    {
        _map = categorizers.ToDictionary(c => c.Type);
    }

    public void Categorize(MediaFile file)
    {
        if (!_map.TryGetValue(file.Type, out var categorizer))
            throw new InvalidOperationException(
                $"No categorizer registered for type {file.Type}");

        categorizer.Categorize(file);
    }
}