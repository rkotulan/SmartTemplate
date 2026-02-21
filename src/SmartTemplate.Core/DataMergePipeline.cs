using SmartTemplate.Core.DataLoaders;
using SmartTemplate.Core.Plugins;

namespace SmartTemplate.Core;

/// <summary>
/// Configuration for <see cref="DataMergePipeline.RunAsync"/>.
/// </summary>
public sealed class DataMergePipelineOptions
{
    public string[] Vars { get; init; } = [];

    /// <summary>When true, interactive prompts are suppressed (for CI).</summary>
    public bool NoInteractive { get; init; }

    /// <summary>
    /// Fully resolved path to the plugins directory, or null when no plugins are used.
    /// Caller is responsible for resolving the path before constructing options.
    /// </summary>
    public string? PluginsDir { get; init; }

    /// <summary>
    /// Reader used for interactive prompts (defaults to <see cref="Console.In"/>).
    /// Override in tests to avoid real stdin.
    /// </summary>
    public TextReader? Reader { get; init; }

    /// <summary>
    /// Writer used for interactive prompts (defaults to <see cref="Console.Out"/>).
    /// Override in tests to capture output.
    /// </summary>
    public TextWriter? Writer { get; init; }
}

/// <summary>
/// Enforces the documented data merge order on an already-loaded data dictionary:
///   1. Interactive prompts (from <c>data["prompts"]</c>)
///   2. CLI <c>--var</c> overrides
///   3. Plugin <see cref="IPlugin.EnrichAsync"/> (in load order)
/// </summary>
/// <remarks>
/// The caller is responsible for loading the data file and resolving the plugins path
/// before calling <see cref="RunAsync"/>.  This allows the caller to extract
/// infrastructure keys (e.g. <c>plugins:</c>) from the raw file data before the
/// pipeline mutates the dictionary.
/// </remarks>
public static class DataMergePipeline
{
    /// <summary>
    /// Runs the merge pipeline on <paramref name="data"/> and returns the enriched
    /// dictionary together with the list of loaded plugins so that the caller can
    /// invoke <see cref="PluginLoader.CleanupPluginsAsync"/> after rendering.
    /// </summary>
    public static async Task<(Dictionary<string, object?> Data, IReadOnlyList<IPlugin> Plugins)> RunAsync(
        Dictionary<string, object?> data,
        DataMergePipelineOptions options,
        CancellationToken ct = default)
    {
        // Step 1: interactive prompts
        var defs = InteractivePrompter.ExtractPrompts(data);
        if (defs.Count > 0 && !options.NoInteractive)
        {
            var writer = options.Writer ?? Console.Out;
            await writer.WriteLineAsync("Zadejte hodnoty proměnných:");
            var prompted = await InteractivePrompter.PromptAsync(defs, options.Reader, options.Writer);
            foreach (var kv in prompted)
                data[kv.Key] = kv.Value;
        }

        // Step 2: CLI --var overrides (highest priority before plugins)
        var cliData = CliVarParser.Parse(options.Vars);
        foreach (var kv in cliData)
            data[kv.Key] = kv.Value;

        // Step 3: plugin enrichment (plugins see fully merged context)
        IReadOnlyList<IPlugin> plugins = [];
        if (options.PluginsDir is not null)
        {
            var loaded = await PluginLoader.LoadPluginsAsync(options.PluginsDir, ct);
            data = await PluginLoader.ApplyPluginsAsync(loaded, data, ct);
            plugins = loaded;
        }

        return (data, plugins);
    }
}
