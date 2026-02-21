using System.Reflection;
using System.Runtime.Loader;

namespace SmartTemplate.Core.Plugins;

public static class PluginLoader
{
    /// <summary>
    /// Scans <paramref name="pluginDirectory"/> for *.dll files, loads each assembly,
    /// and returns all instantiated <see cref="IPlugin"/> implementations found.
    /// Each DLL is loaded in its own <see cref="AssemblyLoadContext"/> so that
    /// dependencies (e.g. Microsoft.Data.SqlClient) are resolved from the plugin's
    /// own directory without conflicting with the host process.
    /// Failures for individual assemblies are written to stderr and skipped.
    /// </summary>
    public static async Task<List<IPlugin>> LoadPluginsAsync(
        string pluginDirectory,
        CancellationToken cancellationToken = default)
    {
        var plugins = new List<IPlugin>();

        if (!Directory.Exists(pluginDirectory))
        {
            await Console.Error.WriteLineAsync(
                $"Warning: plugin directory '{pluginDirectory}' does not exist, skipping plugins.");
            return plugins;
        }

        var dlls = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly);

        foreach (var dll in dlls)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var absoluteDll = Path.GetFullPath(dll);
                var context     = new PluginLoadContext(absoluteDll);
                var assembly    = context.LoadFromAssemblyPath(absoluteDll);
                var pluginType = typeof(IPlugin);

                foreach (var type in assembly.GetExportedTypes())
                {
                    if (!type.IsAbstract && !type.IsInterface && pluginType.IsAssignableFrom(type))
                    {
                        var instance = (IPlugin)Activator.CreateInstance(type)!;
                        plugins.Add(instance);
                    }
                }
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync(
                    $"Warning: failed to load plugin assembly '{dll}': {ex.Message}");
            }
        }

        return plugins;
    }

    /// <summary>
    /// Runs all <paramref name="plugins"/> sequentially, merging the enriched data
    /// into <paramref name="data"/> (plugin output wins on conflict).
    /// </summary>
    public static async Task<Dictionary<string, object?>> ApplyPluginsAsync(
        IEnumerable<IPlugin> plugins,
        Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
    {
        foreach (var plugin in plugins)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var enriched = await plugin.EnrichAsync(data, cancellationToken);
            foreach (var kv in enriched)
                data[kv.Key] = kv.Value;
        }

        return data;
    }
}

/// <summary>
/// Isolated load context for a single plugin DLL.
/// Resolves dependencies from the plugin's own directory first,
/// then falls back to the default (host) context for shared framework assemblies.
/// </summary>
file sealed class PluginLoadContext(string pluginPath) : AssemblyLoadContext(isCollectible: true)
{
    private readonly string _pluginDir = Path.GetDirectoryName(pluginPath)!;
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Shared abstractions that must resolve to the HOST's copy so that
        // type-identity checks (e.g. "is IPlugin") succeed across contexts.
        if (assemblyName.Name is "SmartTemplate.Core")
            return null; // null → delegate to default context

        var resolved = _resolver.ResolveAssemblyToPath(assemblyName);
        if (resolved is not null)
            return LoadFromAssemblyPath(resolved);

        // Fallback: look for the DLL by name in the same directory as the plugin.
        var candidate = Path.Combine(_pluginDir, assemblyName.Name + ".dll");
        if (File.Exists(candidate))
            return LoadFromAssemblyPath(candidate);

        return null; // delegate to default context
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var resolved = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return resolved is not null ? LoadUnmanagedDllFromPath(resolved) : IntPtr.Zero;
    }
}
