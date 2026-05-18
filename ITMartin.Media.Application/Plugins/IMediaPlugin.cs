// File: ITMartin.Media.Application/Abstractions/Plugins/IMediaPlugin.cs

namespace ITMartin.Media.Application.Plugins;

public interface IMediaPlugin
{
    string Name { get; }

    string Version { get; }

    Task InitializeAsync(
        CancellationToken cancellationToken = default);
}