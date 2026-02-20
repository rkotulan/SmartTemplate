using System.Globalization;
using SmartTemplate.Core.Models;

namespace SmartTemplate.Core;

public static class InteractivePrompter
{
    /// <summary>
    /// Extracts prompt definitions from the data dictionary's "prompts" key.
    /// Entries without a non-empty "name" are skipped.
    /// </summary>
    public static List<PromptDefinition> ExtractPrompts(Dictionary<string, object?> data)
    {
        var result = new List<PromptDefinition>();

        if (!data.TryGetValue("prompts", out var raw) || raw is not List<object?> items)
            return result;

        foreach (var item in items)
        {
            if (item is not Dictionary<string, object?> entry)
                continue;

            var name = GetString(entry, "name");
            if (string.IsNullOrWhiteSpace(name))
                continue;

            result.Add(new PromptDefinition
            {
                Name    = name,
                Label   = GetString(entry, "label") is { Length: > 0 } lbl ? lbl : name,
                Type    = GetString(entry, "type") is { Length: > 0 } t ? t : "string",
                Default = GetString(entry, "default"),
                Format  = GetString(entry, "format")
            });
        }

        return result;
    }

    /// <summary>
    /// Prompts the user for each definition and returns the collected values.
    /// Reader/writer default to Console.In/Console.Out for testability.
    /// </summary>
    public static async Task<Dictionary<string, object?>> PromptAsync(
        IEnumerable<PromptDefinition> definitions,
        TextReader? reader = null,
        TextWriter? writer = null)
    {
        reader ??= Console.In;
        writer ??= Console.Out;

        var result = new Dictionary<string, object?>();

        foreach (var def in definitions)
        {
            var isBool = def.Type is "bool" or "boolean";

            string prompt;
            if (isBool)
            {
                var defaultDisplay = def.Default is not null ? $" (výchozí: {def.Default})" : "";
                prompt = $"{def.Label} [y/n]{defaultDisplay}: ";
            }
            else
            {
                var defaultDisplay = def.Default is not null ? $" [{def.Default}]" : "";
                prompt = $"{def.Label}{defaultDisplay}: ";
            }

            await writer.WriteAsync(prompt);
            var input = await reader.ReadLineAsync();

            result[def.Name] = ConvertValue(input?.Trim(), def.Type, def.Default, def.Format);
        }

        return result;
    }

    private static object? ConvertValue(string? input, string type, string? defaultValue, string? format = null)
    {
        var effective = string.IsNullOrEmpty(input) ? defaultValue : input;

        return type switch
        {
            "int" or "integer"   => int.TryParse(effective, out var i) ? i : 0,
            "bool" or "boolean"  => ParseBool(effective),
            "date"               => ParseDate(effective, format),
            _                    => effective ?? ""
        };
    }

    private static bool ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.ToLowerInvariant() is "y" or "yes" or "true" or "1" or "ano" or "a";
    }

    // Built-in accepted formats: dd.MM.yyyy  d.M.yyyy  yyyy-MM-dd  yyyy/MM/dd
    // Always normalizes to yyyy-MM-dd so Scriban's date.parse handles it reliably.
    private static readonly string[] BuiltInDateFormats =
        ["d.M.yyyy", "dd.MM.yyyy", "yyyy-MM-dd", "yyyy/MM/dd"];

    private static string ParseDate(string? value, string? customFormat = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return value ?? "";

        // Custom format takes priority when specified
        if (!string.IsNullOrWhiteSpace(customFormat) &&
            DateTime.TryParseExact(value, customFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt))
            return dt.ToString("yyyy-MM-dd");

        if (DateTime.TryParseExact(value, BuiltInDateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out dt))
            return dt.ToString("yyyy-MM-dd");

        // Fallback: let .NET try with current culture
        if (DateTime.TryParse(value, out dt))
            return dt.ToString("yyyy-MM-dd");

        return value; // Return as-is — Scriban will report the error
    }

    private static string? GetString(Dictionary<string, object?> dict, string key) =>
        dict.TryGetValue(key, out var v) ? v?.ToString() : null;
}
