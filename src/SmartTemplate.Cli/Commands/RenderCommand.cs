using System.CommandLine;

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

    private static readonly Option<bool> ClipOption = new("--clip")
    {
        Description = "Copy rendered output to clipboard (can be combined with --stdout)"
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
            StdoutOption,
            ClipOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            try
            {
                return await RenderExecutor.ExecuteAsync(
                    input:            parseResult.GetValue(InputArg)!,
                    dataFile:         parseResult.GetValue(DataOption),
                    output:           parseResult.GetValue(OutputOption),
                    vars:             parseResult.GetValue(VarOption) ?? [],
                    extension:        parseResult.GetValue(ExtOption)!,
                    noInteractive:    parseResult.GetValue(NoInteractiveOption),
                    cliPluginsDir:    parseResult.GetValue(PluginsOption),
                    toStdout:         parseResult.GetValue(StdoutOption),
                    toClip:           parseResult.GetValue(ClipOption),
                    workingDir:       null,
                    contextDataFiles: null,
                    ct);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                return 1;
            }
        });

        return command;
    }
}
