// File: ITMartin.Media.Infrastructure/Plugins/IsolatedPluginLoader.cs

using System.Reflection;
using ITMartin.Media.Application.Plugins;

namespace ITMartin.Media.Infrastructure.Plugins;

public sealed class IsolatedPluginLoader
    : IPluginLoader
{
    public async Task<IReadOnlyCollection<IMediaPlugin>> LoadAsync(
        string pluginDirectory,
        CancellationToken cancellationToken = default)
    {
        var plugins = new List<IMediaPlugin>();

        if (!Directory.Exists(pluginDirectory))
        {
            return plugins;
        }

        var pluginFiles = Directory.GetFiles(pluginDirectory, "*.dll");

        foreach (var pluginFile in pluginFiles)
        {
            var context = new PluginLoadContext(pluginFile);

            var assembly = context.LoadFromAssemblyPath(
                Path.GetFullPath(pluginFile));

            var pluginTypes = assembly
                .GetTypes()
                .Where(x =>
                    typeof(IMediaPlugin).IsAssignableFrom(x) &&
                    x is { IsAbstract: false, IsInterface: false });

            foreach (var pluginType in pluginTypes)
            {
                if (Activator.CreateInstance(pluginType) is not IMediaPlugin plugin)
                {
                    continue;
                }

                await plugin.InitializeAsync(cancellationToken);

                plugins.Add(plugin);
            }
        }

        return plugins;
    }
}