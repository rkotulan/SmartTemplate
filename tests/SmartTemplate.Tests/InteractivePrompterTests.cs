using SmartTemplate.Core;
using SmartTemplate.Core.Models;

namespace SmartTemplate.Tests;

public class InteractivePrompterTests
{
    // ── ExtractPrompts ────────────────────────────────────────────────────────

    [Fact]
    public void ExtractPrompts_ParsesAllFields()
    {
        var data = new Dictionary<string, object?>
        {
            ["prompts"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["name"]    = "project_name",
                    ["label"]   = "Název projektu",
                    ["type"]    = "string",
                    ["default"] = "MůjProjekt"
                }
            }
        };

        var defs = InteractivePrompter.ExtractPrompts(data);

        Assert.Single(defs);
        Assert.Equal("project_name",  defs[0].Name);
        Assert.Equal("Název projektu", defs[0].Label);
        Assert.Equal("string",         defs[0].Type);
        Assert.Equal("MůjProjekt",     defs[0].Default);
    }

    [Fact]
    public void ExtractPrompts_SkipsEntriesWithoutName()
    {
        var data = new Dictionary<string, object?>
        {
            ["prompts"] = new List<object?>
            {
                new Dictionary<string, object?> { ["label"] = "Missing name" },
                new Dictionary<string, object?> { ["name"] = "valid" }
            }
        };

        var defs = InteractivePrompter.ExtractPrompts(data);

        Assert.Single(defs);
        Assert.Equal("valid", defs[0].Name);
    }

    [Fact]
    public void ExtractPrompts_ReturnsEmpty_WhenNoPromptsKey()
    {
        var data = new Dictionary<string, object?> { ["other"] = "value" };
        var defs = InteractivePrompter.ExtractPrompts(data);
        Assert.Empty(defs);
    }

    [Fact]
    public void ExtractPrompts_LabelFallsBackToName_WhenMissing()
    {
        var data = new Dictionary<string, object?>
        {
            ["prompts"] = new List<object?>
            {
                new Dictionary<string, object?> { ["name"] = "version" }
            }
        };

        var defs = InteractivePrompter.ExtractPrompts(data);

        Assert.Equal("version", defs[0].Label);
    }

    // ── PromptAsync ───────────────────────────────────────────────────────────

    private static async Task<Dictionary<string, object?>> RunPromptAsync(
        IEnumerable<PromptDefinition> defs, string input)
    {
        var reader = new StringReader(input);
        var writer = new StringWriter();
        return await InteractivePrompter.PromptAsync(defs, reader, writer);
    }

    [Fact]
    public async Task PromptAsync_UsesUserInput()
    {
        var defs = new[] { new PromptDefinition { Name = "name", Label = "Name" } };
        var result = await RunPromptAsync(defs, "Alice\n");
        Assert.Equal("Alice", result["name"]);
    }

    [Fact]
    public async Task PromptAsync_UsesDefault_OnEmptyInput()
    {
        var defs = new[] { new PromptDefinition { Name = "env", Label = "Env", Default = "prod" } };
        var result = await RunPromptAsync(defs, "\n");
        Assert.Equal("prod", result["env"]);
    }

    [Fact]
    public async Task PromptAsync_EmptyInput_NoDefault_ReturnsEmptyString()
    {
        var defs = new[] { new PromptDefinition { Name = "desc", Label = "Desc" } };
        var result = await RunPromptAsync(defs, "\n");
        Assert.Equal("", result["desc"]);
    }

    [Fact]
    public async Task PromptAsync_ConvertsInt()
    {
        var defs = new[] { new PromptDefinition { Name = "count", Label = "Count", Type = "int" } };
        var result = await RunPromptAsync(defs, "42\n");
        Assert.Equal(42, result["count"]);
    }

    [Theory]
    [InlineData("y")]
    [InlineData("yes")]
    [InlineData("true")]
    [InlineData("1")]
    [InlineData("ano")]
    [InlineData("a")]
    public async Task PromptAsync_ConvertsBool_TrueVariants(string input)
    {
        var defs = new[] { new PromptDefinition { Name = "flag", Label = "Flag", Type = "bool" } };
        var result = await RunPromptAsync(defs, input + "\n");
        Assert.Equal(true, result["flag"]);
    }

    [Theory]
    [InlineData("n")]
    [InlineData("no")]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("")]
    public async Task PromptAsync_ConvertsBool_FalseVariants(string input)
    {
        var defs = new[] { new PromptDefinition { Name = "flag", Label = "Flag", Type = "bool" } };
        var result = await RunPromptAsync(defs, input + "\n");
        Assert.Equal(false, result["flag"]);
    }
}
