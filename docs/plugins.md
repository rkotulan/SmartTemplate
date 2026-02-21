# Plugins

Plugins are .NET class libraries that enrich the data dictionary before rendering.
They run after all data sources are merged (file → prompts → `--var`), so they always have the full context.

## Managing plugins

### Install

```bash
st plugin install SmartTemplate.Plugin.MoneyErp
st plugin install SmartTemplate.Plugin.MoneyErp --version 1.2.0
st plugin install SmartTemplate.Plugin.MoneyErp --source https://my.feed/v3/index.json
st plugin install SmartTemplate.Plugin.MoneyErp --prerelease
```

Downloads the package and all its transitive NuGet dependencies, then installs everything
to the global plugins folder:

```
%APPDATA%\SmartTemplate\plugins\SmartTemplate.Plugin.MoneyErp\   (Windows)
~/.local/share/SmartTemplate/plugins/SmartTemplate.Plugin.MoneyErp/  (Linux/macOS)
```

### Update

```bash
st plugin update SmartTemplate.Plugin.MoneyErp   # update one plugin
st plugin update                                  # update all installed plugins
```

Removes the existing plugin directory and re-installs the latest version, so stale
dependency DLLs from previous versions are not left behind.

### List / Uninstall

```bash
st plugin list
st plugin uninstall SmartTemplate.Plugin.MoneyErp
```

## Referencing plugins

After installing from NuGet, reference the plugin by name alone in `data.yaml`:

```yaml
plugins: SmartTemplate.Plugin.MoneyErp
```

### Path resolution rules

| `plugins:` value | Resolves to |
|---|---|
| `MoneyErp` (no path separator) | `%APPDATA%\SmartTemplate\plugins\MoneyErp\` |
| `./plugins/MyPlugin/bin/Debug/net10.0/` | relative to the `data.yaml` file |
| `C:\abs\path\` | used as-is |

The CLI `--plugins` option overrides the value in the data file and follows the same rules,
except relative paths are resolved against the current working directory.

## Data merge order

Later sources overwrite earlier ones:

1. YAML / JSON data file
2. Interactive prompt values
3. `--var` CLI overrides
4. Plugin `EnrichAsync` output (applied in plugin load order)

## Writing a plugin

1. Create a .NET class library targeting `net10.0` (or `netstandard2.0`).
2. Reference `SmartTemplate.Core` with `<PrivateAssets>all</PrivateAssets>` — it is
   provided by the host at runtime and must not appear as a NuGet dependency.
3. Add `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` for local usage.
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

### Reference projects

See [`plugins/SmartTemplate.Plugin.Sample`](../plugins/SmartTemplate.Plugin.Sample/) for a
fully annotated skeleton, and
[`plugins/SmartTemplate.Plugin.MoneyErp`](../plugins/SmartTemplate.Plugin.MoneyErp/) for a
real-world example that reads metadata from a SQL Server database.
