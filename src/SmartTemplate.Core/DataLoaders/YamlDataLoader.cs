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
        _ => value.ToString()
    };
}
