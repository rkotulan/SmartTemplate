using System.Runtime.InteropServices;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace SmartTemplate.Cli;

/// <summary>
/// Downloads and installs SmartTemplate plugin packages from NuGet,
/// resolving all transitive dependencies into a flat directory.
/// </summary>
public static class PluginInstaller
{
    public const string DefaultSource = "https://api.nuget.org/v3/index.json";

    /// <summary>
    /// Packages provided by the host (<c>st</c> tool) that must not be installed
    /// into the plugin folder — their types must resolve from the host context.
    /// </summary>
    public static readonly HashSet<string> HostPackages =
        new(StringComparer.OrdinalIgnoreCase) { "SmartTemplate.Core" };

    public static string GlobalPluginsBase =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SmartTemplate", "plugins");

    /// <summary>
    /// Resolves the latest (or specified) version of <paramref name="packageId"/> and
    /// installs it plus all transitive NuGet dependencies into
    /// <c>%APPDATA%\SmartTemplate\plugins\&lt;packageId&gt;\</c>.
    /// </summary>
    public static async Task InstallAsync(
        string packageId,
        string? version,
        string source,
        bool includePrerelease,
        CancellationToken ct)
    {
        var repository = Repository.Factory.GetCoreV3(source);
        using var cache = new SourceCacheContext();
        var logger = NullLogger.Instance;

        var findPkg = await repository.GetResourceAsync<FindPackageByIdResource>(ct);
        var depRes  = await repository.GetResourceAsync<DependencyInfoResource>(ct);

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

        using var pkgStream = new MemoryStream();
        var ok = await findPkg.CopyNupkgToStreamAsync(pkgId.Id, pkgId.Version, pkgStream, cache, logger, ct);
        if (!ok)
        {
            await Console.Out.WriteLineAsync("not found, skipping.");
            return;
        }

        pkgStream.Position = 0;
        using var pkg = new PackageArchiveReader(pkgStream);

        // Extract lib/<best-tfm>/
        var libGroups = pkg.GetLibItems().ToList();
        var bestTfm   = new FrameworkReducer().GetNearest(tfm, libGroups.Select(g => g.TargetFramework));

        if (bestTfm is not null)
        {
            var libGroup = libGroups.First(g => g.TargetFramework == bestTfm);
            foreach (var item in libGroup.Items)
                await ExtractEntryAsync(pkg, item, Path.Combine(installDir, Path.GetFileName(item)), ct);
        }

        // Extract runtimes/<rid>/native/ and runtimes/<rid>/lib/
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

        // Recurse into dependencies
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
