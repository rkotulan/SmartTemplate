# Plugins

Plugins are .NET class libraries that enrich the data dictionary before rendering.
They run after all data sources are merged (file → prompts → `--var`), so they always have the full context.

## Installing a plugin from NuGet

```bash
st plugin install SmartTemplate.Plugin.MoneyErp
st plugin install SmartTemplate.Plugin.MoneyErp --version 1.2.0
st plugin install SmartTemplate.Plugin.MoneyErp --source https://my.feed/v3/index.json
```

The plugin is downloaded and installed to the global plugins folder:

```
%APPDATA%\SmartTemplate\plugins\SmartTemplate.Plugin.MoneyErp\   (Windows)
~/.local/share/SmartTemplate/plugins/SmartTemplate.Plugin.MoneyErp/  (Linux/macOS)
```

Reference it in `data.yaml` by name alone — no path needed:

```yaml
plugins: SmartTemplate.Plugin.MoneyErp
```

### Other plugin commands

```bash
st plugin list                              # list installed plugins
st plugin uninstall SmartTemplate.Plugin.MoneyErp
```

## Using a local plugin

Point `plugins` at a directory containing the plugin DLL and its dependencies.
The path is resolved relative to the data file:

```yaml
# data.yaml
plugins: ./plugins/MyPlugin/bin/Debug/net10.0/
```

Or pass it at the command line (overrides the value in the data file):

```bash
st render templates/ --data data.yaml --plugins ./plugins/
```

## Data merge order

Later sources overwrite earlier ones:

1. YAML / JSON data file
2. Interactive prompt values
3. `--var` CLI overrides
4. Plugin `EnrichAsync` output (applied in plugin load order)

## Writing a plugin

1. Create a .NET class library targeting `net10.0` (or `netstandard2.0`).
2. Reference `SmartTemplate.Core` (NuGet or project reference).
3. Add `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` to the `.csproj`
   so that NuGet dependency DLLs are copied to the output folder.
4. Implement `IPlugin`:

```csharp
using SmartTemplate.Core.Plugins;

public class MyPlugin : IPlugin
{
    public string Name => "MyPlugin";

    public Task<Dictionary<string, object?>> EnrichAsync(
        Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
    {
        data["generated_at"] = DateTime.UtcNow.ToString("yyyy-MM-dd");
        return Task.FromResult(data);
    }
}
```

Plugin load failures are reported as warnings to stderr — other plugins continue loading.

### Reference project

See [`plugins/SmartTemplate.Plugin.Sample`](../plugins/SmartTemplate.Plugin.Sample/) for a
fully annotated skeleton, and
[`plugins/SmartTemplate.Plugin.MoneyErp`](../plugins/SmartTemplate.Plugin.MoneyErp/) for a
real-world example that reads metadata from a SQL Server database.
