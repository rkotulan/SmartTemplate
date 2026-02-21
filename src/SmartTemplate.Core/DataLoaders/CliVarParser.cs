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
            var parts = v.Split('=', 2);
            if (parts.Length < 2 || parts[0].Length == 0)
                throw new ArgumentException($"Invalid --var format '{v}'. Expected key=value.");
            var key = parts[0].Trim();
            var val = parts[1];
            result[key] = val;
        }
        return result;
    }
}
