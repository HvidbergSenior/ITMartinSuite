// File: ITMartin.Media.Infrastructure/Plugins/PluginLoadContext.cs

using System.Reflection;
using System.Runtime.Loader;

namespace ITMartin.Media.Application.Plugins;

public sealed class PluginLoadContext
    : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath)
        : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);

        if (path is null)
        {
            return null;
        }

        return LoadFromAssemblyPath(path);
    }
}