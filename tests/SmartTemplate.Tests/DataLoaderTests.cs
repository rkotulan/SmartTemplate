using SmartTemplate.Core.DataLoaders;
using Xunit;

namespace SmartTemplate.Tests;

public class YamlDataLoaderTests
{
    private readonly YamlDataLoader _loader = new();

    private static async Task<string> TempYamlAsync(string yaml)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".yaml");
        await File.WriteAllTextAsync(path, yaml);
        return path;
    }

    [Fact]
    public async Task LoadAsync_IntValue_PreservesIntType()
    {
        var path = await TempYamlAsync("count: 42");
        try
        {
            var data = await _loader.LoadAsync(path);
            Assert.IsType<int>(data["count"]);
            Assert.Equal(42, data["count"]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task LoadAsync_BoolValue_PreservesBoolType()
    {
        var path = await TempYamlAsync("flag: true");
        try
        {
            var data = await _loader.LoadAsync(path);
            Assert.IsType<bool>(data["flag"]);
            Assert.Equal(true, data["flag"]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task LoadAsync_FloatValue_PreservesDoubleType()
    {
        var path = await TempYamlAsync("rate: 3.14");
        try
        {
            var data = await _loader.LoadAsync(path);
            Assert.IsType<double>(data["rate"]);
            Assert.Equal(3.14, (double)data["rate"]!, precision: 10);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task LoadAsync_NullValue_PreservesNull()
    {
        var path = await TempYamlAsync("empty:");
        try
        {
            var data = await _loader.LoadAsync(path);
            Assert.True(data.ContainsKey("empty"));
            Assert.Null(data["empty"]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task LoadAsync_NestedDict_ConvertedToStringKeyedDict()
    {
        var path = await TempYamlAsync("""
            nested:
              inner: hello
            """);
        try
        {
            var data = await _loader.LoadAsync(path);
            var nested = Assert.IsType<Dictionary<string, object?>>(data["nested"]);
            Assert.Equal("hello", nested["inner"]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task LoadAsync_List_ConvertedToList()
    {
        var path = await TempYamlAsync("""
            items:
              - alpha
              - beta
            """);
        try
        {
            var data = await _loader.LoadAsync(path);
            var list = Assert.IsType<List<object?>>(data["items"]);
            Assert.Equal(2, list.Count);
            Assert.Equal("alpha", list[0]);
            Assert.Equal("beta", list[1]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task LoadAsync_StringValue_IsString()
    {
        var path = await TempYamlAsync("name: Alice");
        try
        {
            var data = await _loader.LoadAsync(path);
            Assert.Equal("Alice", data["name"]);
        }
        finally { File.Delete(path); }
    }
}

public class JsonDataLoaderTests
{
    private readonly JsonDataLoader _loader = new();

    private static async Task<string> TempJsonAsync(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        await File.WriteAllTextAsync(path, json);
        return path;
    }

    [Fact]
    public async Task LoadAsync_IntValue_ReturnedAsLong()
    {
        var path = await TempJsonAsync("""{"count": 99}""");
        try
        {
            var data = await _loader.LoadAsync(path);
            Assert.Equal(99L, data["count"]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task LoadAsync_BoolValue_PreservesBool()
    {
        var path = await TempJsonAsync("""{"active": true}""");
        try
        {
            var data = await _loader.LoadAsync(path);
            Assert.Equal(true, data["active"]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task LoadAsync_NullValue_PreservesNull()
    {
        var path = await TempJsonAsync("""{"x": null}""");
        try
        {
            var data = await _loader.LoadAsync(path);
            Assert.Null(data["x"]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task LoadAsync_NestedObject_ConvertedToDict()
    {
        var path = await TempJsonAsync("""{"nested": {"key": "val"}}""");
        try
        {
            var data = await _loader.LoadAsync(path);
            var nested = Assert.IsType<Dictionary<string, object?>>(data["nested"]);
            Assert.Equal("val", nested["key"]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task LoadAsync_Array_ConvertedToList()
    {
        var path = await TempJsonAsync("""{"tags": ["a","b","c"]}""");
        try
        {
            var data = await _loader.LoadAsync(path);
            var list = Assert.IsType<List<object?>>(data["tags"]);
            Assert.Equal(["a", "b", "c"], list);
        }
        finally { File.Delete(path); }
    }
}

public class CliVarParserTests
{
    [Fact]
    public void Parse_SimpleKeyValue_ReturnsEntry()
    {
        var result = CliVarParser.Parse(["name=Alice"]);
        Assert.Equal("Alice", result["name"]);
    }

    [Fact]
    public void Parse_ValueContainsEquals_PreservesFullValue()
    {
        // Connection strings and URLs often contain '='
        var result = CliVarParser.Parse(["url=http://host?a=1&b=2"]);
        Assert.Equal("http://host?a=1&b=2", result["url"]);
    }

    [Fact]
    public void Parse_EmptyValue_Allowed()
    {
        var result = CliVarParser.Parse(["empty="]);
        Assert.Equal("", result["empty"]);
    }

    [Fact]
    public void Parse_MultipleVars_AllPresent()
    {
        var result = CliVarParser.Parse(["a=1", "b=2", "c=3"]);
        Assert.Equal(3, result.Count);
        Assert.Equal("1", result["a"]);
        Assert.Equal("2", result["b"]);
        Assert.Equal("3", result["c"]);
    }

    [Fact]
    public void Parse_MissingEquals_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => CliVarParser.Parse(["noequalssign"]));
    }

    [Fact]
    public void Parse_EmptyKey_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => CliVarParser.Parse(["=value"]));
    }
}
