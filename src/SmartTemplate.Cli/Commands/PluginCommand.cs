using System.CommandLine;
using System.Runtime.InteropServices;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace SmartTemplate.Cli.Commands;

public static class PluginCommand
{
    private const string DefaultSource = "https://api.nuget.org/v3/index.json";

    /// <summary>
    /// Packages provided by the host (`st` tool) that must not be installed
    /// into the plugin folder — their types must resolve from the host context.
    /// </summary>
    private static readonly HashSet<string> HostPackages =
        new(StringComparer.OrdinalIgnoreCase) { "SmartTemplate.Core" };

    internal static string GlobalPluginsBase =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SmartTemplate", "plugins");

    public static Command Build()
    {
        var cmd = new Command("plugin", "Manage SmartTemplate plugins");
        cmd.Subcommands.Add(BuildInstall());
        cmd.Subcommands.Add(BuildUpdate());
        cmd.Subcommands.Add(BuildList());
        cmd.Subcommands.Add(BuildUninstall());
        return cmd;
    }

    // -------------------------------------------------------------------------
    // install
    // -------------------------------------------------------------------------

    private static Command BuildInstall()
    {
        var packageArg    = new Argument<string>("package")   { Description = "NuGet package ID" };
        var versionOpt    = new Option<string?>("--version")  { Description = "Package version (default: latest stable)" };
        var sourceOpt     = new Option<string?>("--source")   { Description = $"NuGet source URL (default: {DefaultSource})" };
        var prereleaseOpt = new Option<bool>("--prerelease")  { Description = "Allow pre-release versions" };

        var cmd = new Command("install", "Download and install a plugin from NuGet")
            { packageArg, versionOpt, sourceOpt, prereleaseOpt };

        cmd.SetAction(async (parseResult, ct) =>
        {
            var packageId  = parseResult.GetValue(packageArg)!;
            var version    = parseResult.GetValue(versionOpt);
            var source     = parseResult.GetValue(sourceOpt) ?? DefaultSource;
            var prerelease = parseResult.GetValue(prereleaseOpt);

            try
            {
                await InstallAsync(packageId, version, source, prerelease, ct);
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

    private static async Task InstallAsync(
        string packageId, string? version, string source, bool includePrerelease, CancellationToken ct)
    {
        var repository = Repository.Factory.GetCoreV3(source);
        using var cache = new SourceCacheContext();
        var logger = NullLogger.Instance;

        var findPkg = await repository.GetResourceAsync<FindPackageByIdResource>(ct);
        var depRes  = await repository.GetResourceAsync<DependencyInfoResource>(ct);

        // --- resolve root version ---
        NuGetVersion resolvedVersion;
        if (version is not null)
        {
            resolvedVersion = NuGetVersion.Parse(version);
        }
        else
        {
            var allVersions = (await findPkg.GetAllVersionsAsync(packageId, cache, logger, ct)).ToList();
            if (allVersions.Count == 0)
                throw new InvalidOperationException($"Package '{packageId}' not found on {source}.");

            resolvedVersion =
                allVersions.Where(v => includePrerelease || !v.IsPrerelease).DefaultIfEmpty().Max()
                ?? allVersions.Max()!;
        }

        await Console.Out.WriteLineAsync($"Installing {packageId} {resolvedVersion}...");

        var installDir = Path.Combine(GlobalPluginsBase, packageId);
        Directory.CreateDirectory(installDir);

        var targetTfm = NuGetFramework.Parse("net10.0");
        var ridRoots  = GetRidRoots(RuntimeInformation.RuntimeIdentifier);
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await InstallPackageRecursiveAsync(
            findPkg, depRes, cache, logger,
            new PackageIdentity(packageId, resolvedVersion),
            targetTfm, ridRoots, installDir, installed, ct);

        await Console.Out.WriteLineAsync($"Plugin '{packageId}' {resolvedVersion} installed → {installDir}");
        await Console.Out.WriteLineAsync($"Reference in data.yaml:  plugins: {packageId}");
    }

    /// <summary>
    /// Downloads a package and all its transitive NuGet dependencies,
    /// extracting managed DLLs and native runtime assets into <paramref name="installDir"/>.
    /// Host-provided packages (e.g. SmartTemplate.Core) are skipped.
    /// </summary>
    private static async Task InstallPackageRecursiveAsync(
        FindPackageByIdResource findPkg,
        DependencyInfoResource depRes,
        SourceCacheContext cache,
        ILogger logger,
        PackageIdentity pkgId,
        NuGetFramework tfm,
        string[] ridRoots,
        string installDir,
        HashSet<string> installed,
        CancellationToken ct)
    {
        if (HostPackages.Contains(pkgId.Id) || !installed.Add(pkgId.Id))
            return;

        await Console.Out.WriteAsync($"  {pkgId.Id} {pkgId.Version}... ");

        // --- download package ---
        using var pkgStream = new MemoryStream();
        var ok = await findPkg.CopyNupkgToStreamAsync(pkgId.Id, pkgId.Version, pkgStream, cache, logger, ct);
        if (!ok)
        {
            await Console.Out.WriteLineAsync("not found, skipping.");
            return;
        }

        pkgStream.Position = 0;
        using var pkg = new PackageArchiveReader(pkgStream);

        // --- extract lib/<best-tfm>/ ---
        var libGroups = pkg.GetLibItems().ToList();
        var bestTfm   = new FrameworkReducer().GetNearest(tfm, libGroups.Select(g => g.TargetFramework));

        if (bestTfm is not null)
        {
            var libGroup = libGroups.First(g => g.TargetFramework == bestTfm);
            foreach (var item in libGroup.Items)
                await ExtractEntryAsync(pkg, item, Path.Combine(installDir, Path.GetFileName(item)), ct);
        }

        // --- extract runtimes/<rid>/native/ and runtimes/<rid>/lib/ ---
        foreach (var entry in pkg.GetFiles().Where(f => f.StartsWith("runtimes/", StringComparison.Ordinal)))
        {
            foreach (var root in ridRoots)
            {
                if (entry.StartsWith($"runtimes/{root}/native/", StringComparison.Ordinal) ||
                    entry.StartsWith($"runtimes/{root}/lib/",    StringComparison.Ordinal))
                {
                    await ExtractEntryAsync(pkg, entry, Path.Combine(installDir, Path.GetFileName(entry)), ct);
                    break;
                }
            }
        }

        await Console.Out.WriteLineAsync("ok");

        // --- recurse into dependencies ---
        var depInfo = await depRes.ResolvePackage(pkgId, tfm, cache, logger, ct);
        if (depInfo is null) return;

        foreach (var dep in depInfo.Dependencies)
        {
            if (HostPackages.Contains(dep.Id) || installed.Contains(dep.Id))
                continue;

            var versions = (await findPkg.GetAllVersionsAsync(dep.Id, cache, logger, ct))
                .Where(v => !v.IsPrerelease)
                .ToList();
            var bestVer = dep.VersionRange.FindBestMatch(versions);
            if (bestVer is null) continue;

            await InstallPackageRecursiveAsync(
                findPkg, depRes, cache, logger,
                new PackageIdentity(dep.Id, bestVer),
                tfm, ridRoots, installDir, installed, ct);
        }
    }

    // -------------------------------------------------------------------------
    // update
    // -------------------------------------------------------------------------

    private static Command BuildUpdate()
    {
        var packageArg    = new Argument<string?>("package")  { Description = "Plugin package ID to update (omit to update all)", Arity = ArgumentArity.ZeroOrOne };
        var sourceOpt     = new Option<string?>("--source")   { Description = $"NuGet source URL (default: {DefaultSource})" };
        var prereleaseOpt = new Option<bool>("--prerelease")  { Description = "Allow pre-release versions" };

        var cmd = new Command("update", "Update one or all installed plugins to the latest version")
            { packageArg, sourceOpt, prereleaseOpt };

        cmd.SetAction(async (parseResult, ct) =>
        {
            var packageId  = parseResult.GetValue(packageArg);
            var source     = parseResult.GetValue(sourceOpt) ?? DefaultSource;
            var prerelease = parseResult.GetValue(prereleaseOpt);

            var baseDir = GlobalPluginsBase;
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
                    // Remove old files so renamed/removed dependencies don't linger
                    Directory.Delete(installDir, recursive: true);
                    await InstallAsync(id, version: null, source, prerelease, ct);
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

    // -------------------------------------------------------------------------
    // list
    // -------------------------------------------------------------------------

    private static Command BuildList()
    {
        var cmd = new Command("list", "List installed plugins");

        cmd.SetAction(async (_, _) =>
        {
            var baseDir = GlobalPluginsBase;
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

    // -------------------------------------------------------------------------
    // uninstall
    // -------------------------------------------------------------------------

    private static Command BuildUninstall()
    {
        var packageArg = new Argument<string>("package") { Description = "Plugin package ID to remove" };
        var cmd = new Command("uninstall", "Remove an installed plugin") { packageArg };

        cmd.SetAction(async (parseResult, _) =>
        {
            var packageId  = parseResult.GetValue(packageArg)!;
            var installDir = Path.Combine(GlobalPluginsBase, packageId);

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

    // -------------------------------------------------------------------------
    // helpers
    // -------------------------------------------------------------------------

    private static async Task ExtractEntryAsync(
        PackageArchiveReader pkg, string entry, string destPath, CancellationToken ct)
    {
        using var src  = pkg.GetStream(entry);
        using var dest = File.Create(destPath);
        await src.CopyToAsync(dest, ct);
    }

    /// <summary>
    /// Returns RID roots to check for runtime assets, most-specific first.
    /// E.g. "win-x64" → ["win-x64", "win"], "linux-x64" → ["linux-x64", "linux"].
    /// </summary>
    private static string[] GetRidRoots(string rid)
    {
        var dash = rid.IndexOf('-');
        return dash > 0 ? [rid, rid[..dash]] : [rid];
    }
}
