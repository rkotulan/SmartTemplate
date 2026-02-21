using System.CommandLine;

namespace SmartTemplate.Cli.Commands;

public static class PluginCommand
{
    public static Command Build()
    {
        var cmd = new Command("plugin", "Manage SmartTemplate plugins");
        cmd.Subcommands.Add(BuildInstall());
        cmd.Subcommands.Add(BuildUpdate());
        cmd.Subcommands.Add(BuildList());
        cmd.Subcommands.Add(BuildUninstall());
        return cmd;
    }

    private static Command BuildInstall()
    {
        var packageArg    = new Argument<string>("package")   { Description = "NuGet package ID" };
        var versionOpt    = new Option<string?>("--version")  { Description = "Package version (default: latest stable)" };
        var sourceOpt     = new Option<string?>("--source")   { Description = $"NuGet source URL (default: {PluginInstaller.DefaultSource})" };
        var prereleaseOpt = new Option<bool>("--prerelease")  { Description = "Allow pre-release versions" };

        var cmd = new Command("install", "Download and install a plugin from NuGet")
            { packageArg, versionOpt, sourceOpt, prereleaseOpt };

        cmd.SetAction(async (parseResult, ct) =>
        {
            var packageId  = parseResult.GetValue(packageArg)!;
            var version    = parseResult.GetValue(versionOpt);
            var source     = parseResult.GetValue(sourceOpt) ?? PluginInstaller.DefaultSource;
            var prerelease = parseResult.GetValue(prereleaseOpt);

            try
            {
                await PluginInstaller.InstallAsync(packageId, version, source, prerelease, ct);
                return 0;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                return 1;
            }
        });

        return cmd;
    }

    private static Command BuildUpdate()
    {
        var packageArg    = new Argument<string?>("package")  { Description = "Plugin package ID to update (omit to update all)", Arity = ArgumentArity.ZeroOrOne };
        var sourceOpt     = new Option<string?>("--source")   { Description = $"NuGet source URL (default: {PluginInstaller.DefaultSource})" };
        var prereleaseOpt = new Option<bool>("--prerelease")  { Description = "Allow pre-release versions" };

        var cmd = new Command("update", "Update one or all installed plugins to the latest version")
            { packageArg, sourceOpt, prereleaseOpt };

        cmd.SetAction(async (parseResult, ct) =>
        {
            var packageId  = parseResult.GetValue(packageArg);
            var source     = parseResult.GetValue(sourceOpt) ?? PluginInstaller.DefaultSource;
            var prerelease = parseResult.GetValue(prereleaseOpt);

            var baseDir = PluginInstaller.GlobalPluginsBase;
            if (!Directory.Exists(baseDir) || Directory.GetDirectories(baseDir).Length == 0)
            {
                await Console.Out.WriteLineAsync("No plugins installed.");
                return 0;
            }

            var toUpdate = packageId is not null
                ? [packageId]
                : Directory.GetDirectories(baseDir).Select(Path.GetFileName).ToArray();

            var errors = 0;
            foreach (var id in toUpdate)
            {
                if (id is null) continue;
                var installDir = Path.Combine(baseDir, id);
                if (!Directory.Exists(installDir))
                {
                    await Console.Error.WriteLineAsync($"Plugin '{id}' is not installed.");
                    errors++;
                    continue;
                }

                try
                {
                    Directory.Delete(installDir, recursive: true);
                    await PluginInstaller.InstallAsync(id, version: null, source, prerelease, ct);
                }
                catch (Exception ex)
                {
                    await Console.Error.WriteLineAsync($"Error updating '{id}': {ex.Message}");
                    errors++;
                }
            }

            return errors == 0 ? 0 : 1;
        });

        return cmd;
    }

    private static Command BuildList()
    {
        var cmd = new Command("list", "List installed plugins");

        cmd.SetAction(async (_, _) =>
        {
            var baseDir = PluginInstaller.GlobalPluginsBase;
            if (!Directory.Exists(baseDir) || Directory.GetDirectories(baseDir).Length == 0)
            {
                await Console.Out.WriteLineAsync("No plugins installed.");
                return 0;
            }

            await Console.Out.WriteLineAsync($"Installed plugins ({baseDir}):");
            foreach (var dir in Directory.GetDirectories(baseDir).OrderBy(d => d))
                await Console.Out.WriteLineAsync($"  {Path.GetFileName(dir)}");

            return 0;
        });

        return cmd;
    }

    private static Command BuildUninstall()
    {
        var packageArg = new Argument<string>("package") { Description = "Plugin package ID to remove" };
        var cmd = new Command("uninstall", "Remove an installed plugin") { packageArg };

        cmd.SetAction(async (parseResult, _) =>
        {
            var packageId  = parseResult.GetValue(packageArg)!;
            var installDir = Path.Combine(PluginInstaller.GlobalPluginsBase, packageId);

            if (!Directory.Exists(installDir))
            {
                await Console.Error.WriteLineAsync($"Plugin '{packageId}' is not installed.");
                return 1;
            }

            Directory.Delete(installDir, recursive: true);
            await Console.Out.WriteLineAsync($"Plugin '{packageId}' removed.");
            return 0;
        });

        return cmd;
    }
}
