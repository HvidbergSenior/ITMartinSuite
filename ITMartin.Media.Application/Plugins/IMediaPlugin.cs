namespace ITMartin.Media.Application.Plugins;

public interface IMediaPlugin
{
    string Name { get; }

    Task InitializeAsync(CancellationToken cancellationToken);
}