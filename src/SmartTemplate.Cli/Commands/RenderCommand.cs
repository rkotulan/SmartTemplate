using System.CommandLine;
using SmartTemplate.Core;
using SmartTemplate.Core.DataLoaders;
using SmartTemplate.Core.Plugins;

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

    private static readonly Option<string?> PluginsOption = new("--plugins")
    {
        Description = "Path to a directory containing plugin assemblies (*.dll)"
    };

    private static readonly Option<bool> StdoutOption = new("--stdout")
    {
        Description = "Write rendered output to stdout instead of files"
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
            NoInteractiveOption,
            PluginsOption,
            StdoutOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var input         = parseResult.GetValue(InputArg)!;
            var dataFile      = parseResult.GetValue(DataOption);
            var output        = parseResult.GetValue(OutputOption);
            var vars          = parseResult.GetValue(VarOption) ?? [];
            var extension     = parseResult.GetValue(ExtOption)!;
            var noInteractive = parseResult.GetValue(NoInteractiveOption);
            var cliPluginsDir = parseResult.GetValue(PluginsOption);
            var toStdout      = parseResult.GetValue(StdoutOption);

            var engine   = new TemplateEngine();
            var resolver = new OutputResolver(engine);

            try
            {
                // Step 1: load file data
                var data = await DataMerger.LoadFileAsync(dataFile);

                // Step 2: extract 'plugins' key from data dict (CLI --plugins has priority)
                string? pluginsDir = cliPluginsDir;
                if (pluginsDir is null && data.TryGetValue("plugins", out var pluginsVal))
                    pluginsDir = pluginsVal?.ToString();
                data.Remove("plugins");

                // Step 3: interactive prompts (when prompts key exists and not suppressed)
                var defs = InteractivePrompter.ExtractPrompts(data);
                if (defs.Count > 0 && !noInteractive)
                {
                    await Console.Out.WriteLineAsync("Zadejte hodnoty proměnných:");
                    var prompted = await InteractivePrompter.PromptAsync(defs);
                    foreach (var kv in prompted)
                        data[kv.Key] = kv.Value;
                }

                // Step 4: CLI --var overrides (highest priority)
                var cliData = CliVarParser.Parse(vars);
                foreach (var kv in cliData)
                    data[kv.Key] = kv.Value;

                // Step 5: load and apply plugins (after all data is merged — plugins have full context)
                if (pluginsDir is not null)
                {
                    var plugins = await PluginLoader.LoadPluginsAsync(pluginsDir, ct);
                    data = await PluginLoader.ApplyPluginsAsync(plugins, data, ct);
                }

                if (Directory.Exists(input))
                {
                    await RenderDirectoryAsync(engine, resolver, input, data, output, extension, toStdout);
                }
                else if (File.Exists(input))
                {
                    await RenderSingleFileAsync(engine, resolver, input, data, output, outputDir: null, toStdout);
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
        string? outputDir,
        bool toStdout = false)
    {
        var rendered = await engine.RenderFileAsync(inputPath, data);

        if (toStdout)
        {
            await Console.Out.WriteAsync(rendered);
            return;
        }

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
        string extension,
        bool toStdout = false)
    {
        var ext       = extension.StartsWith('.') ? extension : "." + extension;
        var templates = Directory.GetFiles(inputDir, $"*{ext}", SearchOption.AllDirectories);

        if (templates.Length == 0)
        {
            Console.WriteLine($"No *{ext} files found in '{inputDir}'.");
            return;
        }

        foreach (var tmplPath in templates)
        {
            // Preserve subdirectory structure from the template directory
            var relDir = Path.GetDirectoryName(Path.GetRelativePath(inputDir, tmplPath)) ?? "";
            var effectiveOutputDir = (cliOutputDir, relDir) switch
            {
                (not null, not "") => Path.Combine(cliOutputDir, relDir),
                (not null, _)      => cliOutputDir,
                (_, not "")        => relDir,
                _                  => null
            };

            await RenderSingleFileAsync(engine, resolver, tmplPath, data, cliOutput: null, outputDir: effectiveOutputDir, toStdout);
        }
    }
}
