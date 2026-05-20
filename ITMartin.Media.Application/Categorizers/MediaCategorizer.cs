using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Categorizers;

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