namespace ITMartin.Media.Application.Plugins.Models;

public sealed class PluginDescriptor
{
    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string AssemblyPath { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public DateTimeOffset LoadedAt { get; set; }
}