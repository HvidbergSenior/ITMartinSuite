namespace ITMartin.Media.Application.Plugins.Abstractions;

using ITMartin.Media.Application.Plugins.Models;

public interface IPluginLoader
{
    Task<IReadOnlyCollection<PluginDescriptor>>
        LoadAsync(
            CancellationToken cancellationToken);
}