using System.CommandLine;
using SmartTemplate.Core;
using SmartTemplate.Core.DataLoaders;

namespace SmartTemplate.Cli.Commands;

public static class RenderCommand
{
    private static readonly Argument<string> InputArg = new("input")
    {
        Description = "Template file or directory containing .tmpl files"
    };

    private static readonly Option<string?> DataOption = new("--data")
    {
        Description = "Path to a JSON or YAML data file"
    };

    private static readonly Option<string?> OutputOption = new("-o", "--output")
    {
        Description = "Output file or directory path (may itself be a Scriban template string)"
    };

    private static readonly Option<string[]> VarOption = new("--var")
    {
        Description = "Additional variables as key=value pairs",
        AllowMultipleArgumentsPerToken = false
    };

    private static readonly Option<string> ExtOption = new("--ext")
    {
        Description = "Template file extension used when scanning a directory",
        DefaultValueFactory = _ => ".tmpl"
    };

    private static readonly Option<bool> NoInteractiveOption = new("--no-interactive")
    {
        Description = "Skip interactive prompts defined in the data file (for CI/scripting)"
    };

    public static Command Build()
    {
        var command = new Command("render", "Render a template file or directory")
        {
            InputArg,
            DataOption,
            OutputOption,
            VarOption,
            ExtOption,
            NoInteractiveOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var input         = parseResult.GetValue(InputArg)!;
            var dataFile      = parseResult.GetValue(DataOption);
            var output        = parseResult.GetValue(OutputOption);
            var vars          = parseResult.GetValue(VarOption) ?? [];
            var extension     = parseResult.GetValue(ExtOption)!;
            var noInteractive = parseResult.GetValue(NoInteractiveOption);

            var engine   = new TemplateEngine();
            var resolver = new OutputResolver(engine);

            try
            {
                // Step 1: load file data
                var data = await DataMerger.LoadFileAsync(dataFile);

                // Step 2: interactive prompts (when prompts key exists and not suppressed)
                var defs = InteractivePrompter.ExtractPrompts(data);
                if (defs.Count > 0 && !noInteractive)
                {
                    await Console.Out.WriteLineAsync("Zadejte hodnoty proměnných:");
                    var prompted = await InteractivePrompter.PromptAsync(defs);
                    foreach (var kv in prompted)
                        data[kv.Key] = kv.Value;
                }

                // Step 3: CLI --var overrides (highest priority)
                var cliData = CliVarParser.Parse(vars);
                foreach (var kv in cliData)
                    data[kv.Key] = kv.Value;

                if (Directory.Exists(input))
                {
                    await RenderDirectoryAsync(engine, resolver, input, data, output, extension);
                }
                else if (File.Exists(input))
                {
                    await RenderSingleFileAsync(engine, resolver, input, data, output, outputDir: null);
                }
                else
                {
                    await Console.Error.WriteLineAsync($"Error: '{input}' does not exist.");
                    return 1;
                }

                return 0;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                return 1;
            }
        });

        return command;
    }

    private static async Task RenderSingleFileAsync(
        TemplateEngine engine,
        OutputResolver resolver,
        string inputPath,
        Dictionary<string, object?> data,
        string? cliOutput,
        string? outputDir)
    {
        var rendered   = await engine.RenderFileAsync(inputPath, data);
        var outputPath = resolver.Resolve(inputPath, data, cliOutput, outputDir);

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(outputPath, rendered);
        Console.WriteLine($"  {inputPath} -> {outputPath}");
    }

    private static async Task RenderDirectoryAsync(
        TemplateEngine engine,
        OutputResolver resolver,
        string inputDir,
        Dictionary<string, object?> data,
        string? cliOutputDir,
        string extension)
    {
        var ext      = extension.StartsWith('.') ? extension : "." + extension;
        var templates = Directory.GetFiles(inputDir, $"*{ext}", SearchOption.AllDirectories);

        if (templates.Length == 0)
        {
            Console.WriteLine($"No *{ext} files found in '{inputDir}'.");
            return;
        }

        foreach (var tmplPath in templates)
        {
            // Per-file data may override "output", so resolve individually
            await RenderSingleFileAsync(engine, resolver, tmplPath, data, cliOutput: null, outputDir: cliOutputDir);
        }
    }
}
