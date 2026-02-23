using System.Text;
using SmartTemplate.Core;
using SmartTemplate.Core.Plugins;
using TextCopy;

namespace SmartTemplate.Cli.Commands;

/// <summary>
/// Core render pipeline shared by <see cref="RenderCommand"/> and <see cref="RunCommand"/>.
/// </summary>
internal static class RenderExecutor
{
    /// <param name="workingDir">
    /// When non-null, relative <paramref name="input"/>, <paramref name="dataFile"/> and
    /// <paramref name="output"/> paths are resolved against this directory (typically the
    /// directory containing <c>packages.yaml</c>).  When null, the caller is responsible
    /// for passing already-resolved paths.
    /// </param>
    internal static async Task<int> ExecuteAsync(
        string input,
        string? dataFile,
        string? output,
        string[] vars,
        string extension,
        bool noInteractive,
        string? cliPluginsDir,
        bool toStdout,
        bool toClip,
        string? workingDir,
        IReadOnlyList<string>? contextDataFiles,
        CancellationToken ct,
        string? defaultOutputDir = null)
    {
        // Resolve relative paths against workingDir when provided.
        // output may contain Scriban expressions (e.g. "path/{{ solution }}/") — GetFullPath
        // is still safe because {{ and }} are legal path characters on all supported platforms.
        if (workingDir is not null)
        {
            if (!Path.IsPathRooted(input))
                input = Path.GetFullPath(Path.Combine(workingDir, input));

            if (dataFile is not null && !Path.IsPathRooted(dataFile))
                dataFile = Path.GetFullPath(Path.Combine(workingDir, dataFile));

            if (output is not null && !Path.IsPathRooted(output))
                output = Path.GetFullPath(Path.Combine(workingDir, output));
        }

        var engine   = new TemplateEngine();
        var resolver = new OutputResolver(engine);

        // Step 1: load file data
        var data = await DataMerger.LoadFileAsync(dataFile);

        // Merge .st context data files (ordered root → CWD; later = deeper = higher priority)
        if (contextDataFiles is { Count: > 0 })
        {
            foreach (var ctxFile in contextDataFiles)
            {
                var ctxData = await DataMerger.LoadFileAsync(ctxFile);
                foreach (var kv in ctxData)
                    data[kv.Key] = kv.Value;
            }
        }

        // Step 2: extract 'plugins' key from data dict (CLI option has priority)
        string? pluginsDir;
        if (cliPluginsDir is not null)
        {
            pluginsDir = ResolvePluginsPath(cliPluginsDir, baseDir: null);
        }
        else if (data.TryGetValue("plugins", out var pluginsVal) && pluginsVal?.ToString() is { } fromYaml)
        {
            var dataDir = dataFile is not null
                ? Path.GetDirectoryName(Path.GetFullPath(dataFile))
                : workingDir;
            pluginsDir = ResolvePluginsPath(fromYaml, baseDir: dataDir);
        }
        else
        {
            pluginsDir = null;
        }
        data.Remove("plugins");

        // Steps 3-5: prompts → CLI vars → plugin enrichment (enforced order via pipeline)
        var (mergedData, plugins) = await DataMergePipeline.RunAsync(data, new DataMergePipelineOptions
        {
            Vars          = vars,
            NoInteractive = noInteractive,
            PluginsDir    = pluginsDir,
        }, ct);
        data = mergedData;

        // Step 6: render
        StringBuilder? clipAccumulator = toClip ? new StringBuilder() : null;

        if (Directory.Exists(input))
        {
            await RenderDirectoryAsync(engine, resolver, input, data, output ?? defaultOutputDir, extension, toStdout, clipAccumulator);
        }
        else if (File.Exists(input))
        {
            await RenderSingleFileAsync(engine, resolver, input, data, output,
                outputDir: output is null ? defaultOutputDir : null,
                toStdout, clipAccumulator);
        }
        else
        {
            await Console.Error.WriteLineAsync($"Error: '{input}' does not exist.");
            await PluginLoader.CleanupPluginsAsync(plugins);
            return 1;
        }

        await PluginLoader.CleanupPluginsAsync(plugins);

        if (clipAccumulator is not null)
        {
            try
            {
                await ClipboardService.SetTextAsync(clipAccumulator.ToString(), ct);
                await Console.Error.WriteLineAsync("Copied to clipboard.");
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Warning: could not write to clipboard: {ex.Message}");
            }
        }

        return 0;
    }

    internal static string ResolvePluginsPath(string value, string? baseDir)
    {
        if (Path.IsPathRooted(value))
            return value;

        if (!value.Contains('/') && !value.Contains('\\'))
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SmartTemplate", "plugins", value);

        var anchor = baseDir ?? Directory.GetCurrentDirectory();
        return Path.GetFullPath(Path.Combine(anchor, value));
    }

    internal static async Task RenderSingleFileAsync(
        TemplateEngine engine,
        OutputResolver resolver,
        string inputPath,
        Dictionary<string, object?> data,
        string? cliOutput,
        string? outputDir,
        bool toStdout = false,
        StringBuilder? clipAccumulator = null)
    {
        var rendered = await engine.RenderFileAsync(inputPath, data);

        clipAccumulator?.Append(rendered);

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
        Console.Write($"  {Path.GetFileName(inputPath)}");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($" -> ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"{outputPath}  ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("done");
        Console.ResetColor();
    }

    internal static async Task RenderDirectoryAsync(
        TemplateEngine engine,
        OutputResolver resolver,
        string inputDir,
        Dictionary<string, object?> data,
        string? cliOutputDir,
        string extension,
        bool toStdout = false,
        StringBuilder? clipAccumulator = null)
    {
        if (cliOutputDir is not null)
        {
            cliOutputDir = cliOutputDir.TrimEnd('"');
            cliOutputDir = engine.Render(cliOutputDir, data);
        }

        var ext       = extension.StartsWith('.') ? extension : "." + extension;
        var templates = Directory.GetFiles(inputDir, $"*{ext}", SearchOption.AllDirectories);

        if (templates.Length == 0)
        {
            Console.WriteLine($"No *{ext} files found in '{inputDir}'.");
            return;
        }

        foreach (var tmplPath in templates)
        {
            var relDir = Path.GetDirectoryName(Path.GetRelativePath(inputDir, tmplPath)) ?? "";
            if (relDir.Length > 0 && relDir.Contains("{{"))
            {
                var sep = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
                relDir = string.Join(
                    Path.DirectorySeparatorChar,
                    relDir.Split(sep, StringSplitOptions.RemoveEmptyEntries)
                          .Select(seg => seg.Contains("{{") ? engine.Render(seg, data) : seg));
            }

            var effectiveOutputDir = (cliOutputDir, relDir) switch
            {
                (not null, not "") => Path.Combine(cliOutputDir, relDir),
                (not null, _)      => cliOutputDir,
                (_, not "")        => relDir,
                _                  => null
            };

            await RenderSingleFileAsync(engine, resolver, tmplPath, data,
                cliOutput: null, outputDir: effectiveOutputDir, toStdout, clipAccumulator);
        }
    }
}
