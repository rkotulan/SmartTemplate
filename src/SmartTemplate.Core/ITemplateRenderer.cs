namespace SmartTemplate.Core;

/// <summary>Renders a Scriban template string against a data dictionary.</summary>
public interface ITemplateRenderer
{
    string Render(string template, Dictionary<string, object?> data);
}
