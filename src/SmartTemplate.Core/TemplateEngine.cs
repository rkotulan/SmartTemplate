using Scriban;
using Scriban.Functions;
using Scriban.Runtime;

namespace SmartTemplate.Core;

public class TemplateEngine
{
    /// <summary>
    /// Renders a Scriban template string using the provided data dictionary.
    /// </summary>
    public string Render(string templateContent, Dictionary<string, object?> data)
    {
        var template = Template.Parse(templateContent);
        if (template.HasErrors)
        {
            var errors = string.Join(Environment.NewLine, template.Messages.Select(m => m.ToString()));
            throw new InvalidOperationException($"Template parse errors:{Environment.NewLine}{errors}");
        }

        var context = new TemplateContext { StrictVariables = false };

        // Build an extended date object: start from Scriban's built-in DateTimeFunctions
        // (which provides date.now, date.to_string, date.add_days, date.add_months, etc.)
        // and then add our custom extras like date.today.
        var dateObj = new DateTimeFunctions();
        dateObj.SetValue("today", DateTime.Today, readOnly: false);

        // Root script object containing user-supplied data variables
        var scriptObject = new ScriptObject();
        scriptObject.SetValue("date", dateObj, readOnly: false);
        foreach (var kv in data)
            scriptObject.SetValue(kv.Key, kv.Value, readOnly: false);

        context.PushGlobal(scriptObject);

         return template.Render(context);
    }

    /// <summary>
    /// Renders a Scriban template file using the provided data dictionary.
    /// </summary>
    public async Task<string> RenderFileAsync(string templatePath, Dictionary<string, object?> data)
    {
        var content = await File.ReadAllTextAsync(templatePath);
        return Render(content, data);
    }
}
