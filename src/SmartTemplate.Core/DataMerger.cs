using SmartTemplate.Core.DataLoaders;

namespace SmartTemplate.Core;

public static class DataMerger
{
    private static readonly JsonDataLoader JsonLoader = new();
    private static readonly YamlDataLoader YamlLoader = new();

    /// <summary>
    /// Loads data from a YAML or JSON file. Returns an empty dictionary when dataFile is null/empty.
    /// </summary>
    public static async Task<Dictionary<string, object?>> LoadFileAsync(string? dataFile)
    {
        if (string.IsNullOrWhiteSpace(dataFile))
            return new Dictionary<string, object?>();

        if (YamlLoader.CanLoad(dataFile))
            return await YamlLoader.LoadAsync(dataFile);
        if (JsonLoader.CanLoad(dataFile))
            return await JsonLoader.LoadAsync(dataFile);

        throw new NotSupportedException($"Unsupported data file format: {dataFile}. Use .json, .yaml, or .yml.");
    }

    /// <summary>
    /// Loads and merges data in order: data file (YAML/JSON) then CLI --var overrides.
    /// Later entries overwrite earlier ones.
    /// </summary>
    public static async Task<Dictionary<string, object?>> MergeAsync(
        string? dataFile,
        IEnumerable<string> cliVars)
    {
        var merged = await LoadFileAsync(dataFile);

        var cliData = CliVarParser.Parse(cliVars);
        foreach (var kv in cliData)
            merged[kv.Key] = kv.Value;

        return merged;
    }
}
