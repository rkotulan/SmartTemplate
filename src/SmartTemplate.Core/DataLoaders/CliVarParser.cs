namespace SmartTemplate.Core.DataLoaders;

public static class CliVarParser
{
    /// <summary>
    /// Parses --var key=value entries into a dictionary.
    /// Supports nested keys with dot notation: key.sub=value
    /// </summary>
    public static Dictionary<string, object?> Parse(IEnumerable<string> vars)
    {
        var result = new Dictionary<string, object?>();
        foreach (var v in vars)
        {
            var idx = v.IndexOf('=');
            if (idx <= 0)
                throw new ArgumentException($"Invalid --var format '{v}'. Expected key=value.");
            var key = v[..idx].Trim();
            var val = v[(idx + 1)..];
            result[key] = val;
        }
        return result;
    }
}
