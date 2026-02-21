using System.Globalization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SmartTemplate.Core.DataLoaders;

public class YamlDataLoader : IDataLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(NullNamingConvention.Instance)
        .Build();

    public bool CanLoad(string source) =>
        source.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
        source.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);

    public async Task<Dictionary<string, object?>> LoadAsync(string source)
    {
        var yaml = await File.ReadAllTextAsync(source);
        var parsed = Deserializer.Deserialize<Dictionary<object, object?>>(yaml);
        return ConvertDict(parsed);
    }

    private static Dictionary<string, object?> ConvertDict(Dictionary<object, object?> dict)
    {
        var result = new Dictionary<string, object?>();
        foreach (var kv in dict)
            result[kv.Key.ToString()!] = ConvertValue(kv.Value);
        return result;
    }

    private static object? ConvertValue(object? value) => value switch
    {
        null => null,
        Dictionary<object, object?> d => ConvertDict(d),
        List<object?> list => list.Select(ConvertValue).ToList(),
        string s => ParseScalar(s),
        _ => value   // already typed by YamlDotNet (future-proof)
    };

    /// <summary>
    /// Infers the .NET type of an unquoted YAML scalar.
    /// YamlDotNet 16.x deserializes all scalars as <see cref="string"/> when the
    /// target type is <see cref="object"/>; this method applies YAML 1.2 type rules.
    /// Priority: bool → int → long → double → string.
    /// </summary>
    private static object? ParseScalar(string s)
    {
        if (bool.TryParse(s, out var b)) return b;
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;
        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
        if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var d)) return d;
        return s;
    }
}
