namespace SmartTemplate.Core.Models;

/// <summary>
/// Represents a single template package defined in <c>packages.yaml</c>.
/// </summary>
public sealed class PackageDefinition
{
    /// <summary>Short machine-readable identifier used on the command line (e.g. "3d", "fr").</summary>
    public string Id { get; set; } = "";

    /// <summary>Human-readable display name shown in the interactive selection menu.</summary>
    public string Name { get; set; } = "";

    /// <summary>Path to the YAML/JSON data file (relative to <c>packages.yaml</c>).</summary>
    public string? Data { get; set; }

    /// <summary>Path to the template file or directory (relative to <c>packages.yaml</c>).</summary>
    public string Templates { get; set; } = "";

    /// <summary>Output file or directory path. May contain Scriban expressions.</summary>
    public string? Output { get; set; }

    /// <summary>Write rendered output to stdout instead of files.</summary>
    public bool Stdout { get; set; }

    /// <summary>Copy rendered output to clipboard.</summary>
    public bool Clip { get; set; }

    /// <summary>Skip interactive prompts (for CI / scripting).</summary>
    public bool NoInteractive { get; set; }

    /// <summary>Plugin directory or named global plugin.</summary>
    public string? Plugins { get; set; }

    /// <summary>Additional key=value overrides applied on top of the data file.</summary>
    public List<string>? Vars { get; set; }
}
