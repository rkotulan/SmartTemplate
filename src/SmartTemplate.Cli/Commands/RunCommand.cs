using System.CommandLine;
using SmartTemplate.Core;
using SmartTemplate.Core.Models;

namespace SmartTemplate.Cli.Commands;

public static class RunCommand
{
    private const string DefaultConfig = "packages.yaml";
    private const string StDir         = ".st";

    private static readonly Argument<string?> PackageArg = new("package")
    {
        Description = "Package ID to run (omit to pick interactively)",
        Arity = ArgumentArity.ZeroOrOne
    };

    private static readonly Option<string?> ConfigOption = new("--config")
    {
        Description = $"Path to the packages config file. " +
                      $"When omitted, searches for '{DefaultConfig}' in the current directory, " +
                      $"then for '.st/{DefaultConfig}' walking up the directory tree."
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
            var packageId      = parseResult.GetValue(PackageArg);
            var explicitConfig = parseResult.GetValue(ConfigOption);
            var invocationCwd  = Directory.GetCurrentDirectory();

            var configPath = FindConfig(explicitConfig);

            if (configPath is null)
            {
                if (explicitConfig is not null)
                    await Console.Error.WriteLineAsync($"Error: '{explicitConfig}' not found.");
                else
                    await Console.Error.WriteLineAsync(
                        $"Error: '{DefaultConfig}' not found in current directory " +
                        $"and no '.st/{DefaultConfig}' found in the directory tree.");
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

            // When no output path is defined in the package, fall back to the directory
            // where the user invoked 'st run' (not the config file's directory).
            var defaultOutputDir = string.IsNullOrWhiteSpace(selected.Output) ? invocationCwd : null;

            var contextFilename = selected.Context
                ?? (selected.Data is not null ? Path.GetFileName(selected.Data) : null);

            var contextDataFiles = contextFilename is not null
                ? FindContextDataFiles(contextFilename, configDir, invocationCwd)
                : Array.Empty<string>();

            try
            {
                return await RenderExecutor.ExecuteAsync(
                    input:            selected.Templates,
                    dataFile:         selected.Data,
                    output:           selected.Output,
                    vars:             selected.Vars?.ToArray() ?? [],
                    extension:        ".tmpl",
                    noInteractive:    selected.NoInteractive,
                    cliPluginsDir:    selected.Plugins,
                    toStdout:         selected.Stdout,
                    toClip:           selected.Clip,
                    workingDir:       configDir,
                    contextDataFiles: contextDataFiles,
                    ct,
                    defaultOutputDir: defaultOutputDir);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                return 1;
            }
        });

        return cmd;
    }

    /// <summary>
    /// Walks from <paramref name="startDir"/> up to (and including) the effective search root
    /// derived from <paramref name="configDir"/>, collecting all paths where
    /// <c>.st/<paramref name="filename"/></c> exists.
    /// Result is ordered root-first so that the deepest directory (CWD) has the highest priority
    /// when merged in order.
    /// </summary>
    internal static IReadOnlyList<string> FindContextDataFiles(
        string filename, string configDir, string startDir)
    {
        // If configDir itself is a .st/ dir (e.g. ".st/packages.yaml" case),
        // the search root is its parent; otherwise it is configDir itself.
        var configDirInfo = new DirectoryInfo(configDir);
        var searchRoot = string.Equals(configDirInfo.Name, StDir,
            StringComparison.OrdinalIgnoreCase)
            ? configDirInfo.Parent!.FullName
            : configDirInfo.FullName;

        var found = new List<string>();
        var dir   = new DirectoryInfo(Path.GetFullPath(startDir));

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, StDir, filename);
            if (File.Exists(candidate))
                found.Add(candidate);

            if (string.Equals(dir.FullName, searchRoot, StringComparison.OrdinalIgnoreCase))
                break;

            // Stop if CWD has somehow wandered above the search root.
            if (!dir.FullName.StartsWith(searchRoot, StringComparison.OrdinalIgnoreCase))
                break;

            dir = dir.Parent!;
        }

        found.Reverse(); // root → CWD order (deepest = highest priority when merged last)
        return found;
    }

    /// <summary>
    /// Resolves the config file path.
    /// If <paramref name="explicitPath"/> is provided, returns it only if the file exists.
    /// Otherwise searches for <c>packages.yaml</c> in the current directory first,
    /// then walks up the directory tree looking for a <c>.st/packages.yaml</c> at each level.
    /// </summary>
    private static string? FindConfig(string? explicitPath)
    {
        if (explicitPath is not null)
            return File.Exists(explicitPath) ? Path.GetFullPath(explicitPath) : null;

        // 1. packages.yaml in the current directory (backward-compatible fast path)
        if (File.Exists(DefaultConfig))
            return Path.GetFullPath(DefaultConfig);

        // 2. Walk up the tree: look for .st/packages.yaml at each level
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, StDir, DefaultConfig);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        return null;
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
        await Console.Out.WriteLineAsync($"  {packages.Count + 1}) Exit");

        await Console.Out.WriteAsync($"Select package (1-{packages.Count + 1}): ");

        var line = await Console.In.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(line)) return null;

        if (!int.TryParse(line.Trim(), out var idx) || idx < 1 || idx > packages.Count + 1)
        {
            await Console.Error.WriteLineAsync("Invalid selection.");
            return null;
        }

        if (idx == packages.Count + 1) return null;

        return packages[idx - 1];
    }
}
