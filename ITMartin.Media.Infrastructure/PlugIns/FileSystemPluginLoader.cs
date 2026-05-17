namespace ITMartin.Media.Infrastructure.Plugins;

using System.Reflection;
using ITMartin.Media.Application.Plugins.Models;
using ITMartin.Media.Application.Plugins.Abstractions;

public sealed class FileSystemPluginLoader
    : IPluginLoader
{
    private readonly string _pluginFolder;

    public FileSystemPluginLoader()
    {
        _pluginFolder =
            Path.Combine(
                AppContext.BaseDirectory,
                "plugins");

        Directory.CreateDirectory(
            _pluginFolder);
    }

    public Task<IReadOnlyCollection<PluginDescriptor>>
        LoadAsync(
            CancellationToken cancellationToken)
    {
        IReadOnlyCollection<PluginDescriptor>
            plugins =
                Directory
                    .EnumerateFiles(
                        _pluginFolder,
                        "*.dll",
                        SearchOption.TopDirectoryOnly)
                    .Select(x =>
                        new PluginDescriptor
                        {
                            Name =
                                Path.GetFileNameWithoutExtension(x),

                            Version =
                                AssemblyName
                                    .GetAssemblyName(x)
                                    .Version?
                                    .ToString()
                                ?? "1.0.0",

                            AssemblyPath = x,

                            Enabled = true,

                            LoadedAt =
                                DateTimeOffset.UtcNow
                        })
                    .ToList();

        return Task.FromResult(plugins);
    }
}