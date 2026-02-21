using System.CommandLine;
using SmartTemplate.Core;
using SmartTemplate.Core.Models;

namespace SmartTemplate.Cli.Commands;

public static class RunCommand
{
    private const string DefaultConfig = "packages.yaml";

    private static readonly Argument<string?> PackageArg = new("package")
    {
        Description = "Package ID to run (omit to pick interactively)",
        Arity = ArgumentArity.ZeroOrOne
    };

    private static readonly Option<string> ConfigOption = new("--config")
    {
        Description = $"Path to the packages config file (default: {DefaultConfig})",
        DefaultValueFactory = _ => DefaultConfig
    };

    public static Command Build()
    {
        var cmd = new Command("run", $"Run a template package defined in {DefaultConfig}")
        {
            PackageArg,
            ConfigOption
        };

        cmd.SetAction(async (parseResult, ct) =>
        {
            var packageId  = parseResult.GetValue(PackageArg);
            var configPath = parseResult.GetValue(ConfigOption)!;

            if (!File.Exists(configPath))
            {
                await Console.Error.WriteLineAsync($"Error: '{configPath}' not found. Create a packages.yaml in the current directory.");
                return 1;
            }

            var configDir = Path.GetDirectoryName(Path.GetFullPath(configPath))!;

            List<PackageDefinition> packages;
            try
            {
                packages = await PackageConfigLoader.LoadAsync(configPath);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error reading '{configPath}': {ex.Message}");
                return 1;
            }

            if (packages.Count == 0)
            {
                await Console.Error.WriteLineAsync("No packages defined in packages.yaml.");
                return 1;
            }

            PackageDefinition? selected;

            if (packageId is not null)
            {
                selected = packages.FirstOrDefault(p =>
                    string.Equals(p.Id, packageId, StringComparison.OrdinalIgnoreCase));

                if (selected is null)
                {
                    await Console.Error.WriteLineAsync($"Package '{packageId}' not found.");
                    await Console.Error.WriteLineAsync("Available: " + string.Join(", ", packages.Select(p => p.Id)));
                    return 1;
                }
            }
            else
            {
                selected = await PromptPackageAsync(packages);
                if (selected is null) return 0;
            }

            try
            {
                return await RenderExecutor.ExecuteAsync(
                    input:         selected.Templates,
                    dataFile:      selected.Data,
                    output:        selected.Output,
                    vars:          selected.Vars?.ToArray() ?? [],
                    extension:     ".tmpl",
                    noInteractive: selected.NoInteractive,
                    cliPluginsDir: selected.Plugins,
                    toStdout:      selected.Stdout,
                    toClip:        selected.Clip,
                    workingDir:    configDir,
                    ct);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                return 1;
            }
        });

        return cmd;
    }

    private static async Task<PackageDefinition?> PromptPackageAsync(List<PackageDefinition> packages)
    {
        await Console.Out.WriteLineAsync("Available packages:");
        for (var i = 0; i < packages.Count; i++)
        {
            var p     = packages[i];
            var label = string.IsNullOrWhiteSpace(p.Name) ? p.Id : $"{p.Name}  [{p.Id}]";
            await Console.Out.WriteLineAsync($"  {i + 1}) {label}");
        }

        await Console.Out.WriteAsync($"Select package (1-{packages.Count}): ");

        var line = await Console.In.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(line)) return null;

        if (!int.TryParse(line.Trim(), out var idx) || idx < 1 || idx > packages.Count)
        {
            await Console.Error.WriteLineAsync("Invalid selection.");
            return null;
        }

        return packages[idx - 1];
    }
}
