// File: ITMartin.Media.Application/Plugins/Models/PluginDescriptor.cs

namespace ITMartin.Media.Application.Plugins.Models;

public sealed class PluginDescriptor
{
    public required string Name { get; init; }

    public required string Version { get; init; }

    public required string AssemblyPath { get; init; }

    public bool Enabled { get; init; }

    public DateTimeOffset LoadedAt { get; init; }
}