using SmartTemplate.Core;
using Xunit;

namespace SmartTemplate.Tests;

public class OutputResolverTests
{
    private readonly TemplateEngine _engine = new();
    private OutputResolver Resolver => new(_engine);

    [Fact]
    public void Resolve_CliOutputSpecified_UsesCli()
    {
        var result = Resolver.Resolve("template.tmpl", [], "output.txt");
        Assert.Equal("output.txt", result);
    }

    [Fact]
    public void Resolve_DataOutputSpecified_UsesData()
    {
        var data = new Dictionary<string, object?> { ["output"] = "from_data.txt" };
        var result = Resolver.Resolve("template.tmpl", data, null);
        Assert.Equal("from_data.txt", result);
    }

    [Fact]
    public void Resolve_NoOutput_StripsTmplExtension()
    {
        var result = Resolver.Resolve("report.tmpl", [], null);
        Assert.Equal("report", result);
    }

    [Fact]
    public void Resolve_NoOutput_NonTmplFile_KeepsOriginalName()
    {
        var result = Resolver.Resolve("readme.md", [], null);
        Assert.Equal("readme.md", result);
    }

    [Fact]
    public void Resolve_CliOutputIsTemplate_RendersIt()
    {
        var data = new Dictionary<string, object?> { ["name"] = "report" };
        var result = Resolver.Resolve("t.tmpl", data, "{{ name }}.txt");
        Assert.Equal("report.txt", result);
    }

    [Fact]
    public void Resolve_DataOutputIsTemplate_RendersIt()
    {
        var data = new Dictionary<string, object?> { ["name"] = "myfile", ["output"] = "{{ name }}.md" };
        var result = Resolver.Resolve("t.tmpl", data, null);
        Assert.Equal("myfile.md", result);
    }

    [Fact]
    public void Resolve_WithOutputDirectory_CombinesPaths()
    {
        var result = Resolver.Resolve("template.tmpl", [], null, "out");
        Assert.Equal(Path.Combine("out", "template"), result);
    }

    [Fact]
    public void Resolve_CliPriority_OverridesDataOutput()
    {
        var data = new Dictionary<string, object?> { ["output"] = "data_output.txt" };
        var result = Resolver.Resolve("template.tmpl", data, "cli_output.txt");
        Assert.Equal("cli_output.txt", result);
    }
}
