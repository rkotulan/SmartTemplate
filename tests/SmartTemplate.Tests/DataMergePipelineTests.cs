using SmartTemplate.Core;
using Xunit;

namespace SmartTemplate.Tests;

public class DataMergePipelineTests
{
    // Helper: data dictionary with a single string prompt definition.
    private static Dictionary<string, object?> DataWithPrompt(string name, string? defaultVal = null)
    {
        var entry = new Dictionary<string, object?> { ["name"] = name, ["label"] = name };
        if (defaultVal is not null)
            entry["default"] = defaultVal;

        return new Dictionary<string, object?>
        {
            ["prompts"] = new List<object?> { entry }
        };
    }

    [Fact]
    public async Task RunAsync_NoInteractive_SkipsPrompts()
    {
        var data = DataWithPrompt("answer");

        // No reader — if prompts ran they would block on Console.In
        var (result, _) = await DataMergePipeline.RunAsync(data, new DataMergePipelineOptions
        {
            NoInteractive = true
        });

        Assert.False(result.ContainsKey("answer"), "Prompt value should not be present when skipped.");
    }

    [Fact]
    public async Task RunAsync_Interactive_AppliesPromptResult()
    {
        var data = DataWithPrompt("answer");
        var reader = new StringReader("hello\n");
        var writer = new StringWriter();

        var (result, _) = await DataMergePipeline.RunAsync(data, new DataMergePipelineOptions
        {
            NoInteractive = false,
            Reader = reader,
            Writer = writer
        });

        Assert.Equal("hello", result["answer"]);
    }

    [Fact]
    public async Task RunAsync_CliVarsOverrideFileData()
    {
        var data = new Dictionary<string, object?> { ["key"] = "original" };

        var (result, _) = await DataMergePipeline.RunAsync(data, new DataMergePipelineOptions
        {
            Vars = ["key=overridden"],
            NoInteractive = true
        });

        Assert.Equal("overridden", result["key"]);
    }

    [Fact]
    public async Task RunAsync_CliVarsOverridePromptResult()
    {
        // Even when the prompt answers "from_prompt", the CLI var wins.
        var data = DataWithPrompt("x");
        var reader = new StringReader("from_prompt\n");
        var writer = new StringWriter();

        var (result, _) = await DataMergePipeline.RunAsync(data, new DataMergePipelineOptions
        {
            Vars = ["x=from_var"],
            NoInteractive = false,
            Reader = reader,
            Writer = writer
        });

        Assert.Equal("from_var", result["x"]);
    }

    [Fact]
    public async Task RunAsync_MergeOrder_DataBeforeVars()
    {
        // File data has a=file; CLI var sets a=cli.  CLI wins.
        var data = new Dictionary<string, object?> { ["a"] = "file", ["b"] = "file_b" };

        var (result, _) = await DataMergePipeline.RunAsync(data, new DataMergePipelineOptions
        {
            Vars = ["a=cli"],
            NoInteractive = true
        });

        Assert.Equal("cli",    result["a"]);
        Assert.Equal("file_b", result["b"]); // untouched
    }

    [Fact]
    public async Task RunAsync_NoPluginsDir_ReturnsEmptyPluginList()
    {
        var (_, plugins) = await DataMergePipeline.RunAsync(
            new Dictionary<string, object?>(),
            new DataMergePipelineOptions { NoInteractive = true });

        Assert.Empty(plugins);
    }

    [Fact]
    public async Task RunAsync_PromptWithDefault_UsedWhenInputBlank()
    {
        var data = DataWithPrompt("color", defaultVal: "blue");
        var reader = new StringReader("\n"); // empty input → use default
        var writer = new StringWriter();

        var (result, _) = await DataMergePipeline.RunAsync(data, new DataMergePipelineOptions
        {
            NoInteractive = false,
            Reader = reader,
            Writer = writer
        });

        Assert.Equal("blue", result["color"]);
    }
}
