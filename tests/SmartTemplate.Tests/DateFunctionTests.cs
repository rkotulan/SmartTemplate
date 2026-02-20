using SmartTemplate.Core;
using Xunit;

namespace SmartTemplate.Tests;

public class DateFunctionTests
{
    [Fact]
    public void Parse_IsoString_ReturnsCorrectDate()
    {
        var result = DateFunctions.Parse("2024-06-15");
        Assert.Equal(new DateTime(2024, 6, 15), result.Date);
    }

    [Fact]
    public void Parse_Today_ReturnsToday()
    {
        var result = DateFunctions.Parse("today");
        Assert.Equal(DateTime.Today, result);
    }

    [Fact]
    public void Parse_Now_ReturnsApproximatelyNow()
    {
        var before = DateTime.Now;
        var result = DateFunctions.Parse("now");
        var after  = DateTime.Now;
        Assert.InRange(result, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public void Parse_EmptyString_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => DateFunctions.Parse(""));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(30)]
    public void AddDays_AddsCorrectly(int days)
    {
        var date   = new DateTime(2024, 1, 15);
        var result = DateFunctions.AddDays(date, days);
        Assert.Equal(date.AddDays(days), result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(12)]
    public void AddMonths_AddsCorrectly(int months)
    {
        var date   = new DateTime(2024, 3, 10);
        var result = DateFunctions.AddMonths(date, months);
        Assert.Equal(date.AddMonths(months), result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-5)]
    public void AddYears_AddsCorrectly(int years)
    {
        var date   = new DateTime(2023, 7, 4);
        var result = DateFunctions.AddYears(date, years);
        Assert.Equal(date.AddYears(years), result);
    }

    [Fact]
    public void Format_WithStrftimePattern_FormatsCorrectly()
    {
        var date   = new DateTime(2024, 6, 15);
        var result = DateFunctions.Format(date, "%Y-%m-%d");
        Assert.Equal("2024-06-15", result);
    }
}
