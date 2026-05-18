// File: ITMartin.Media.Application/Plugins/Abstractions/IPluginLoader.cs

using ITMartin.Media.Application.Plugins.Models;

namespace ITMartin.Media.Application.Plugins.Abstractions;

public interface IPluginLoader
{
    Task<IReadOnlyCollection<PluginDescriptor>>
        LoadAsync(
            string pluginDirectory,
            CancellationToken cancellationToken = default);
}