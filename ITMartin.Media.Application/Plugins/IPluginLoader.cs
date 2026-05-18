// File: ITMartin.Media.Application/Abstractions/Plugins/IPluginLoader.cs

namespace ITMartin.Media.Application.Plugins;

public interface IPluginLoader
{
    Task<IReadOnlyCollection<IMediaPlugin>> LoadAsync(
        string pluginDirectory,
        CancellationToken cancellationToken = default);
}