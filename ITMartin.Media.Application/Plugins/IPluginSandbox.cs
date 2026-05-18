// File: ITMartin.Media.Application/Abstractions/Plugins/IPluginSandbox.cs

namespace ITMartin.Media.Application.Plugins;

public interface IPluginSandbox
{
    Task ExecuteAsync(
        IMediaPlugin plugin,
        CancellationToken cancellationToken = default);
}