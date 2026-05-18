// File: ITMartin.Media.Infrastructure/Plugins/AssemblyPluginLoader.cs

using System.Reflection;

namespace ITMartin.Media.Application.Plugins;

public sealed class AssemblyPluginLoader
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

        var assemblies = Directory.GetFiles(pluginDirectory, "*.dll");

        foreach (var assemblyPath in assemblies)
        {
            var assembly = Assembly.LoadFrom(assemblyPath);

            var pluginTypes = assembly
                .GetTypes()
                .Where(x =>
                    typeof(IMediaPlugin).IsAssignableFrom(x) &&
                    x is { IsInterface: false, IsAbstract: false });

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