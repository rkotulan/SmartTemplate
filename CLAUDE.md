# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Tech stack

- **Runtime**: .NET 10 / `net10.0`
- **Template engine**: Scriban 6.x — uses `{{ }}` for both expressions and control flow (not `{% %}`)
- **CLI framework**: System.CommandLine 3.0-preview
- **Data**: YamlDotNet 16.x, System.Text.Json
- **NuGet client**: NuGet.Protocol 6.x + NuGet.Packaging 6.x (in SmartTemplate.Cli)
- **Tests**: xUnit

## Build & test

```bash
dotnet build SmartTemplate.slnx /nodeReuse:false
dotnet test  SmartTemplate.slnx /nodeReuse:false --no-build
```

Always pass `/nodeReuse:false` to avoid MSBuild mutex conflicts with IDE background builds.

## Repository structure

```
SmartTemplate/
├── src/
│   ├── SmartTemplate.Core/           # Core library (no CLI dependency)
│   │   ├── Plugins/
│   │   │   ├── IPlugin.cs            # Plugin contract
│   │   │   └── PluginLoader.cs       # Assembly loading with PluginLoadContext
│   │   ├── DataLoaders/              # YAML, JSON, CLI var parsers
│   │   ├── DataMerger.cs             # Merges file / prompts / --var sources
│   │   ├── InteractivePrompter.cs    # Runtime prompts from data["prompts"]
│   │   ├── TemplateEngine.cs         # Scriban wrapper
│   │   ├── OutputResolver.cs         # Output path resolution logic
│   │   └── DateFunctions.cs          # Custom Scriban date extensions
│   └── SmartTemplate.Cli/            # `st` global dotnet tool
│       ├── Program.cs
│       └── Commands/
│           ├── RenderCommand.cs      # st render
│           └── PluginCommand.cs      # st plugin install / list / uninstall
├── plugins/
│   ├── SmartTemplate.Plugin.Sample/  # Annotated skeleton for plugin authors
│   └── SmartTemplate.Plugin.MoneyErp/ # SQL Server metadata → template vars
├── tests/
│   └── SmartTemplate.Tests/          # xUnit — 56 tests
├── samples/
│   └── money-erp-form-relations/     # End-to-end MoneyErp example
└── docs/
    ├── templates.md
    ├── prompts.md
    └── plugins.md
```

## Key conventions

### System.CommandLine 3.0-preview API
- `rootCommand.Subcommands.Add(cmd)` — not `AddCommand`
- `command.SetAction(async (parseResult, ct) => { ... return exitCode; })`
- `parseResult.GetValue(option)`

### Scriban
- Control flow: `{{ if cond }}...{{ end }}`, `{{ for x in list }}...{{ end }}`
- Whitespace stripping: `{{~` / `~}}` strips adjacent whitespace/newlines

### Data merge order (later wins)
1. YAML / JSON file (`--data`)
2. Interactive prompts (`data["prompts"]`)
3. CLI `--var key=value` overrides
4. Plugin `EnrichAsync` output (in load order)

### Plugin system
- `IPlugin` in `SmartTemplate.Core.Plugins` — implement `Name` + `EnrichAsync`
- Loaded via `PluginLoadContext` (isolated `AssemblyLoadContext` per DLL)
- **TPA check**: assemblies in `TRUSTED_PLATFORM_ASSEMBLIES` always delegate to host (prevents DiagnosticSource and similar version conflicts)
- **Native DLL skip**: `IsManagedAssembly()` uses `AssemblyName.GetAssemblyName()` to skip native DLLs silently
- `SmartTemplate.Core` always delegates to host for type-identity
- Plugin csproj must have `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` for local use
- Plugin csproj: reference `SmartTemplate.Core` with `<PrivateAssets>all</PrivateAssets>` so it is not listed as a NuGet dependency

### Plugin path resolution (in `RenderCommand`)
| `plugins:` value | Resolves to |
|---|---|
| Absolute path | used as-is |
| Relative path (`./...`) | relative to `data.yaml` directory |
| Name only (`MoneyErp`) | `%APPDATA%\SmartTemplate\plugins\<name>\` |

### `st plugin install`
- Downloads package + all transitive NuGet dependencies recursively
- Extracts `lib/<best-tfm>/` and `runtimes/<rid>/native/` assets
- Skips packages in `HostPackages` set (currently: `SmartTemplate.Core`)
- Global install dir: `%APPDATA%\SmartTemplate\plugins\<PackageId>\`

## CI/CD

`.github/workflows/publish.yml` — triggers on push to `main`:
1. Build + test solution
2. `dotnet pack` → `SmartTemplate` (CLI tool) + `SmartTemplate.Plugin.MoneyErp`
3. Push all `.nupkg` to NuGet.org using `NUGET_API_KEY` secret
