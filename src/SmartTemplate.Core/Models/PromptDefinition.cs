namespace SmartTemplate.Core.Models;

public sealed class PromptDefinition
{
    public string  Label   { get; init; } = "";
    public string  Name    { get; init; } = "";
    public string  Type    { get; init; } = "string";
    public string? Default { get; init; }
    public string? Format  { get; init; }
}
