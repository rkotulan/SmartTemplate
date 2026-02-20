using System.Text.Json;

namespace SmartTemplate.Core.DataLoaders;

public class JsonDataLoader : IDataLoader
{
    public bool CanLoad(string source) =>
        source.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    public async Task<Dictionary<string, object?>> LoadAsync(string source)
    {
        var json = await File.ReadAllTextAsync(source);
        var doc = JsonDocument.Parse(json);
        return FlattenElement(doc.RootElement);
    }

    private static Dictionary<string, object?> FlattenElement(JsonElement element)
    {
        var result = new Dictionary<string, object?>();
        if (element.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var prop in element.EnumerateObject())
            result[prop.Name] = ConvertElement(prop.Value);

        return result;
    }

    internal static object? ConvertElement(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String  => el.GetString(),
        JsonValueKind.Number  => el.TryGetInt64(out var l) ? (object)l : el.GetDouble(),
        JsonValueKind.True    => true,
        JsonValueKind.False   => false,
        JsonValueKind.Null    => null,
        JsonValueKind.Array   => el.EnumerateArray().Select(ConvertElement).ToList(),
        JsonValueKind.Object  => FlattenElement(el),
        _ => el.GetRawText()
    };
}
