using SmartTemplate.Cli.Commands;
using SmartTemplate.Core;

namespace SmartTemplate.Tests;

/// <summary>
/// Tests for the .st/{filename} hierarchical context-data-file merge feature in RunCommand.
/// </summary>
public class RunCommandContextTests : IDisposable
{
    private readonly string _root;

    public RunCommandContextTests()
    {
        // Create a fresh temp directory tree for each test:
        //   root/
        //     .st/packages.yaml    (config lives here)
        //     .st/pkg.yaml         (root context)
        //     sub/
        //       .st/pkg.yaml       (intermediate context)
        //       deep/              (CWD)
        //         .st/pkg.yaml     (deepest context)
        _root = Path.Combine(Path.GetTempPath(), "SmartTemplate_Tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch { /* best effort */ }
    }

    // ------------------------------------------------------------------ helpers

    private string StDir(string dir)
    {
        var path = Path.Combine(_root, dir, ".st");
        Directory.CreateDirectory(path);
        return path;
    }

    private void WriteYaml(string dir, string filename, string yaml)
    {
        var stPath = Path.Combine(_root, dir, ".st");
        Directory.CreateDirectory(stPath);
        File.WriteAllText(Path.Combine(stPath, filename), yaml);
    }

    private string FullPath(string relative) => Path.Combine(_root, relative);

    // ------------------------------------------------------------------ FindContextDataFiles tests

    [Fact]
    public void FindContextDataFiles_ReturnsEmpty_WhenNoneExist()
    {
        // No .st/pkg.yaml files anywhere — expect empty result.
        var configDir = _root; // config is at root (not in a .st/ subdir)
        var startDir  = _root;

        var result = RunCommand.FindContextDataFiles("pkg.yaml", configDir, startDir);

        Assert.Empty(result);
    }

    [Fact]
    public void FindContextDataFiles_FindsFilesRootToDeep()
    {
        // Create .st/pkg.yaml at root, sub, and sub/deep levels.
        WriteYaml("",       "pkg.yaml", "namespace: RootNs\n");
        WriteYaml("sub",    "pkg.yaml", "namespace: SubNs\n");
        WriteYaml("sub/deep", "pkg.yaml", "namespace: DeepNs\n");

        // configDir = root (packages.yaml is at root, not inside .st/)
        var configDir = _root;
        var startDir  = FullPath("sub/deep");

        var result = RunCommand.FindContextDataFiles("pkg.yaml", configDir, startDir);

        Assert.Equal(3, result.Count);
        // Ordered root → deep (root first, deepest last = highest priority when merged)
        Assert.EndsWith(Path.Combine(".st", "pkg.yaml"), result[0]);
        Assert.Contains("sub",  result[1]);
        Assert.Contains("deep", result[2]);
    }

    [Fact]
    public void FindContextDataFiles_StopsAtSearchRoot()
    {
        // Create a .st/pkg.yaml one level ABOVE the project root — should NOT be included.
        var aboveRoot = Path.GetDirectoryName(_root)!;
        var aboveFile = Path.Combine(aboveRoot, ".st", "pkg.yaml");
        Directory.CreateDirectory(Path.GetDirectoryName(aboveFile)!);
        File.WriteAllText(aboveFile, "namespace: AboveNs\n");

        // Create one inside root
        WriteYaml("", "pkg.yaml", "namespace: RootNs\n");

        var configDir = _root;
        var startDir  = _root;

        var result = RunCommand.FindContextDataFiles("pkg.yaml", configDir, startDir);

        // Only the root-level file; the above-root file must NOT appear.
        Assert.Single(result);
        Assert.DoesNotContain(aboveFile, result);

        // Cleanup extra file
        File.Delete(aboveFile);
    }

    [Fact]
    public void FindContextDataFiles_WorksWhenCwdEqualsSearchRoot()
    {
        // CWD == configDir → should find exactly 1 file (the root-level context).
        WriteYaml("", "pkg.yaml", "namespace: RootNs\n");

        var configDir = _root;
        var startDir  = _root;

        var result = RunCommand.FindContextDataFiles("pkg.yaml", configDir, startDir);

        Assert.Single(result);
        Assert.EndsWith(Path.Combine(".st", "pkg.yaml"), result[0],
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindContextDataFiles_ConfigInStDir_UsesParentAsSearchRoot()
    {
        // When the config itself lives inside .st/ (the typical case),
        // the search root should be the .st/ parent (i.e. _root), not _root/.st/.
        WriteYaml("",    "pkg.yaml", "namespace: RootNs\n");
        WriteYaml("sub", "pkg.yaml", "namespace: SubNs\n");

        // Simulate: config is at _root/.st/packages.yaml
        var configDir = Path.Combine(_root, ".st");

        var result = RunCommand.FindContextDataFiles("pkg.yaml", configDir, FullPath("sub"));

        // Both root and sub should be found.
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Contains("sub"));
    }

    // ------------------------------------------------------------------ merge-order tests

    [Fact]
    public async Task MergeOrder_DeepestWins()
    {
        // root context: namespace=RootNs
        // deep context: namespace=DeepNs
        // After merge: DeepNs wins (root → deep order, later overwrites earlier)
        WriteYaml("",       "pkg.yaml", "namespace: RootNs\n");
        WriteYaml("sub/deep", "pkg.yaml", "namespace: DeepNs\n");

        var contextFiles = RunCommand.FindContextDataFiles(
            "pkg.yaml", _root, FullPath("sub/deep"));

        // Simulate the merge that RenderExecutor performs
        var data = new Dictionary<string, object?>();
        foreach (var ctxFile in contextFiles)
        {
            var ctxData = await DataMerger.LoadFileAsync(ctxFile);
            foreach (var kv in ctxData)
                data[kv.Key] = kv.Value;
        }

        Assert.Equal("DeepNs", data["namespace"]);
    }

    [Fact]
    public async Task MergeOrder_PackageDataLowestPriority()
    {
        // Package data file sets namespace=PackageNs.
        // A context file sets namespace=ContextNs.
        // After merge: ContextNs wins.
        var pkgDataFile = Path.Combine(_root, "defaults.yaml");
        File.WriteAllText(pkgDataFile, "namespace: PackageNs\n");

        WriteYaml("", "pkg.yaml", "namespace: ContextNs\n");

        var contextFiles = RunCommand.FindContextDataFiles("pkg.yaml", _root, _root);

        // Load package data (lowest priority)
        var data = await DataMerger.LoadFileAsync(pkgDataFile);

        // Merge context files on top
        foreach (var ctxFile in contextFiles)
        {
            var ctxData = await DataMerger.LoadFileAsync(ctxFile);
            foreach (var kv in ctxData)
                data[kv.Key] = kv.Value;
        }

        Assert.Equal("ContextNs", data["namespace"]);
    }
}
