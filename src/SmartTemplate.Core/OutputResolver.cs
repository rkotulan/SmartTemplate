namespace SmartTemplate.Core;

public class OutputResolver
{
    private readonly TemplateEngine _engine;

    public OutputResolver(TemplateEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Resolves the output path for a single template file.
    /// Priority: CLI -o > data["output"] > strip .tmpl extension from input filename.
    /// Both -o and data["output"] are treated as Scriban templates themselves.
    /// </summary>
    public string Resolve(
        string inputPath,
        Dictionary<string, object?> data,
        string? cliOutput,
        string? outputDirectory = null)
    {
        string outputPath;

        if (!string.IsNullOrWhiteSpace(cliOutput))
        {
            // -o might be a template itself (e.g. "report_{{ date.now | date.to_string '%Y%m%d' }}.txt")
            outputPath = _engine.Render(cliOutput, data);
        }
        else if (data.TryGetValue("output", out var outputValue) && outputValue is string outputTemplate && !string.IsNullOrWhiteSpace(outputTemplate))
        {
            outputPath = _engine.Render(outputTemplate, data);
        }
        else
        {
            // Default: strip template extension, then render filename as a template.
            // This allows filenames like "{{ krok }}.md.tmpl" → "K01.md".
            var fileName = Path.GetFileName(inputPath);
            if (fileName.EndsWith(".tmpl", StringComparison.OrdinalIgnoreCase))
                fileName = fileName[..^5];
            outputPath = _engine.Render(fileName, data);
        }

        // If outputDirectory is specified and outputPath is not absolute, place file there
        if (!string.IsNullOrWhiteSpace(outputDirectory) && !Path.IsPathRooted(outputPath))
            outputPath = Path.Combine(outputDirectory, outputPath);

        return outputPath;
    }
}
