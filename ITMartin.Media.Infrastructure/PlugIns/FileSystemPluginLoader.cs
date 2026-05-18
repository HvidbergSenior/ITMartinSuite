// File: ITMartin.Media.Infrastructure.Plugins/FileSystemPluginLoader.cs

using System.Reflection;
using ITMartin.Media.Application.Plugins.Abstractions;
using ITMartin.Media.Application.Plugins.Models;

namespace ITMartin.Media.Infrastructure.Plugins;


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
            string pluginDirectory,
            CancellationToken cancellationToken = default)
    {
        var folder =
            string.IsNullOrWhiteSpace(pluginDirectory)
                ? _pluginFolder
                : pluginDirectory;

        Directory.CreateDirectory(folder);

        IReadOnlyCollection<PluginDescriptor>
            plugins =
                Directory
                    .EnumerateFiles(
                        folder,
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