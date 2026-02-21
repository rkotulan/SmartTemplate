using System.Reflection;
using System.Runtime.Loader;

namespace SmartTemplate.Core.Plugins;

public static class PluginLoader
{
    /// <summary>
    /// Scans <paramref name="pluginDirectory"/> for *.dll files, loads each managed assembly,
    /// and returns all instantiated <see cref="IPlugin"/> implementations found.
    /// Each DLL is loaded in its own <see cref="AssemblyLoadContext"/> so that
    /// dependencies (e.g. Microsoft.Data.SqlClient) are resolved from the plugin's
    /// own directory without conflicting with the host process.
    /// Native DLLs are silently skipped. Failures for individual assemblies are written
    /// to stderr and skipped.
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

            // Skip native (unmanaged) DLLs — they cannot be loaded as .NET assemblies
            // and are handled by the OS loader when the plugin calls P/Invoke.
            if (!IsManagedAssembly(dll))
                continue;

            try
            {
                var absoluteDll = Path.GetFullPath(dll);
                var context     = new PluginLoadContext(absoluteDll);
                var assembly    = context.LoadFromAssemblyPath(absoluteDll);
                var pluginType  = typeof(IPlugin);

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

    /// <summary>
    /// Returns true when the file at <paramref name="path"/> is a managed .NET assembly.
    /// Native DLLs throw <see cref="BadImageFormatException"/> from
    /// <see cref="AssemblyName.GetAssemblyName"/>.
    /// </summary>
    private static bool IsManagedAssembly(string path)
    {
        try
        {
            AssemblyName.GetAssemblyName(path);
            return true;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
        catch
        {
            return true; // conservative: let the loader report the real error
        }
    }
}

/// <summary>
/// Isolated load context for a single plugin DLL.
/// Resolution priority:
///   1. Trusted platform assemblies (shared .NET framework) → host
///   2. SmartTemplate.Core → host (type-identity across contexts)
///   3. Plugin's own deps.json → plugin directory
///   4. Plugin directory by filename → plugin directory
///   5. Everything else → host (default context)
/// </summary>
file sealed class PluginLoadContext(string pluginPath) : AssemblyLoadContext(isCollectible: true)
{
    private readonly string _pluginDir = Path.GetDirectoryName(pluginPath)!;
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

    /// <summary>
    /// Names of assemblies that are part of the .NET shared framework on this machine.
    /// These must always resolve from the host to avoid API version mismatches.
    /// </summary>
    private static readonly HashSet<string> TrustedPlatformAssemblies = BuildTpaSet();

    private static HashSet<string> BuildTpaSet()
    {
        var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "";
        return tpa
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name ?? "";

        // Delegate .NET shared-framework assemblies to the host to avoid
        // API version mismatches (e.g. System.Diagnostics.DiagnosticSource).
        if (TrustedPlatformAssemblies.Contains(name))
            return null;

        // SmartTemplate.Core must resolve from the host so that IPlugin type-identity
        // checks succeed across load contexts.
        if (name is "SmartTemplate.Core")
            return null;

        // Plugin's own deps.json — authoritative for plugin-specific dependencies.
        var resolved = _resolver.ResolveAssemblyToPath(assemblyName);
        if (resolved is not null)
            return LoadFromAssemblyPath(resolved);

        // Fallback: DLL present in the plugin directory but not in deps.json.
        var candidate = Path.Combine(_pluginDir, name + ".dll");
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
