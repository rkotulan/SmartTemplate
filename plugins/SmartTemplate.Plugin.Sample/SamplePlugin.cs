using SmartTemplate.Core.Plugins;

namespace SmartTemplate.Plugin.Sample;

/// <summary>
/// Minimal reference implementation of <see cref="IPlugin"/>.
///
/// A plugin receives the fully-merged data dictionary (after the data file,
/// interactive prompts, and --var overrides have all been applied) and returns
/// an enriched copy.  Keys returned by the plugin overwrite existing keys of
/// the same name — earlier sources lose on conflict.
///
/// HOW TO USE
/// ----------
/// 1. Build this project:
///      dotnet build plugins/SmartTemplate.Plugin.Sample/
///
/// 2. Copy (or let MSBuild copy via a post-build event) the output DLL to a
///    directory of your choice, e.g. my-project/plugins/.
///
/// 3. Run st with the --plugins flag:
///      st render templates/ --data data.yaml --plugins my-project/plugins/
///
///    Or declare the directory in data.yaml so you don't need the CLI flag:
///      plugins: my-project/plugins/
///
/// HOW TO WRITE YOUR OWN PLUGIN
/// ----------------------------
/// 1. Create a .NET class library targeting net10.0.
/// 2. Add a PackageReference to SmartTemplate.Core (once it is on NuGet),
///    or a ProjectReference if you work in the same solution.
/// 3. Implement IPlugin (this file is the template).
/// 4. Build → copy the DLL → run st.
/// </summary>
public sealed class SamplePlugin : IPlugin
{
    /// <summary>
    /// Unique name shown in diagnostic output.  Use a descriptive, stable identifier.
    /// </summary>
    public string Name => "Sample";

    /// <summary>
    /// Enrich the template data dictionary.
    ///
    /// <paramref name="data"/> already contains all merged values from:
    ///   - the data file (YAML / JSON)
    ///   - interactive prompts
    ///   - --var CLI overrides
    ///
    /// You may read existing keys to derive new ones, call external systems
    /// (databases, REST APIs, file systems …), or add completely new keys.
    ///
    /// Return the same dictionary instance — or a new one — with the keys you
    /// want to expose in templates.  Keys present in the returned dictionary
    /// will be merged into the final data set (returned keys win on conflict).
    /// </summary>
    public Task<Dictionary<string, object?>> EnrichAsync(
        Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
    {
        // ------------------------------------------------------------------
        // Example 1: add a computed value derived from existing data
        // ------------------------------------------------------------------
        if (data.TryGetValue("entity", out var entity) && entity is string entityName)
        {
            data["entity_lower"] = entityName.ToLowerInvariant();
            data["entity_upper"] = entityName.ToUpperInvariant();
        }

        // ------------------------------------------------------------------
        // Example 2: inject a value that is always present
        // ------------------------------------------------------------------
        data["generated_by"] = $"SmartTemplate/{Name}Plugin";
        data["generated_at"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        // ------------------------------------------------------------------
        // Example 3: read from an external source (database, API, file …)
        // Uncomment and adapt — async/await works fine here.
        // ------------------------------------------------------------------
        // var properties = await MyDatabase.GetEntityPropertiesAsync(entityName, cancellationToken);
        // data["properties"] = properties;

        return Task.FromResult(data);
    }
}
