using SmartTemplate.Core;
using Xunit;

namespace SmartTemplate.Tests;

public class TemplateEngineTests
{
    private readonly TemplateEngine _engine = new();

    [Fact]
    public void Render_SimpleVariable_ReturnsSubstitutedValue()
    {
        var data = new Dictionary<string, object?> { ["name"] = "World" };
        var result = _engine.Render("Hello {{ name }}!", data);
        Assert.Equal("Hello World!", result);
    }

    [Fact]
    public void Render_MultipleVariables_AllSubstituted()
    {
        var data = new Dictionary<string, object?>
        {
            ["first"] = "John",
            ["last"] = "Doe"
        };
        var result = _engine.Render("{{ first }} {{ last }}", data);
        Assert.Equal("John Doe", result);
    }

    [Fact]
    public void Render_NumberVariable_ReturnsNumber()
    {
        var data = new Dictionary<string, object?> { ["count"] = 42L };
        var result = _engine.Render("Count: {{ count }}", data);
        Assert.Equal("Count: 42", result);
    }

    [Fact]
    public void Render_IfCondition_WorksCorrectly()
    {
        var data = new Dictionary<string, object?> { ["show"] = true };
        var result = _engine.Render("{{ if show }}visible{{ end }}", data);
        Assert.Equal("visible", result.Trim());
    }

    [Fact]
    public void Render_ForLoop_IteratesCollection()
    {
        var data = new Dictionary<string, object?>
        {
            ["items"] = new List<object?> { "a", "b", "c" }
        };
        var result = _engine.Render("{{ for item in items }}{{ item }}{{ end }}", data);
        Assert.Equal("abc", result.Trim());
    }

    [Fact]
    public void Render_StringFilter_UpperCase()
    {
        var data = new Dictionary<string, object?> { ["word"] = "hello" };
        var result = _engine.Render("{{ word | string.upcase }}", data);
        Assert.Equal("HELLO", result);
    }

    [Fact]
    public void Render_MissingVariable_ReturnsEmpty()
    {
        var result = _engine.Render("{{ missing_var }}", new Dictionary<string, object?>());
        Assert.Equal("", result.Trim());
    }

    [Fact]
    public void Render_InvalidTemplate_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _engine.Render("{{ func(", new Dictionary<string, object?>()));
    }

    [Fact]
    public void Render_DateNow_ReturnsCurrentYear()
    {
        var result = _engine.Render("{{ date.now | date.to_string '%Y' }}", new Dictionary<string, object?>());
        Assert.Equal(DateTime.Now.Year.ToString(), result.Trim());
    }

    [Fact]
    public void Render_CustomDateParse_Works()
    {
        var result = _engine.Render("{{ date.parse '2024-06-15' | date.to_string '%Y-%m-%d' }}", new Dictionary<string, object?>());
        Assert.Equal("2024-06-15", result.Trim());
    }

    [Fact]
    public void Render_CustomDateAddDays_Works()
    {
        var result = _engine.Render(
            "{{ date.parse '2024-01-01' | date.add_days 10 | date.to_string '%Y-%m-%d' }}",
            new Dictionary<string, object?>());
        Assert.Equal("2024-01-11", result.Trim());
    }

    [Fact]
    public void Render_CustomDateAddMonths_Works()
    {
        var result = _engine.Render(
            "{{ date.parse '2024-01-15' | date.add_months 2 | date.to_string '%Y-%m-%d' }}",
            new Dictionary<string, object?>());
        Assert.Equal("2024-03-15", result.Trim());
    }

    [Fact]
    public void Render_CustomDateAddYears_Works()
    {
        var result = _engine.Render(
            "{{ date.parse '2023-05-20' | date.add_years 1 | date.to_string '%Y-%m-%d' }}",
            new Dictionary<string, object?>());
        Assert.Equal("2024-05-20", result.Trim());
    }
}
